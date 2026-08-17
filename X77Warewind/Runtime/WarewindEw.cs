using System;
using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>Directed jam — capacitor + direct seeker jamAccumulation inject.</summary>
    internal static class WarewindEw
    {
        private static readonly FieldInfo? PowerField =
            typeof(JammingPod).GetField("power", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? FalloffField =
            typeof(JammingPod).GetField("rangeFalloff", BindingFlags.Instance | BindingFlags.NonPublic);

        private static AnimationCurve? _falloff;
        private static float _refPower = 800f;
        private static float _lastLog;

        internal static void Cache(Encyclopedia enc)
        {
            if (enc?.aircraft == null || _falloff != null)
                return;
            foreach (AircraftDefinition ad in enc.aircraft)
            {
                if (ad?.unitPrefab == null)
                    continue;
                JammingPod[] pods = ad.unitPrefab.GetComponentsInChildren<JammingPod>(true);
                for (int i = 0; i < pods.Length; i++)
                {
                    if (pods[i] == null)
                        continue;
                    _falloff = FalloffField?.GetValue(pods[i]) as AnimationCurve;
                    object? p = PowerField?.GetValue(pods[i]);
                    if (p is float watts && watts > 1f)
                    {
                        _refPower = watts;
                        WarewindPlugin.ModLog?.LogInfo($"Warewind EW from '{ad.jsonKey}' power={watts:F0}");
                    }
                    if (_falloff != null)
                        return;
                }
            }
        }

        internal static void EnsureAntenna(Missile missile, WarewindFlight flight)
        {
            if (missile == null || flight == null)
                return;
            if (IsRotatableAntenna(flight.EwDummy))
                return;

            Transform? vis = WarewindVisualStamp.FindVisual(missile.transform);
            Transform parent = vis != null ? vis : missile.transform;
            Transform? existing = parent.Find(WarewindConstants.EwAntennaName);
            if (IsRotatableAntenna(existing))
            {
                flight.EwDummy = existing;
                return;
            }

            GameObject go = new GameObject(WarewindConstants.EwAntennaName);
            Transform ant = go.transform;
            ant.SetParent(parent, false);
            ant.localPosition = Vector3.zero;
            ant.localRotation = Quaternion.identity;
            flight.EwDummy = ant;
        }

        internal static bool IsRotatableAntenna(Transform? t)
        {
            if (t == null)
                return false;
            if (t.GetComponent<Renderer>() != null)
                return false;
            if (t.GetComponentInChildren<Renderer>(true) != null)
                return false;
            string n = t.name ?? string.Empty;
            if (string.Equals(n, WarewindConstants.VisualRootName, StringComparison.OrdinalIgnoreCase))
                return false;
            if (NameInList(n, WarewindConstants.Stage1Aliases) || NameInList(n, WarewindConstants.Stage2Aliases))
                return false;
            return true;
        }

        internal static void Tick(Missile self, WarewindFlight flight)
        {
            if (self == null || flight == null)
                return;

            EnsureAntenna(self, flight);

            float dt = Time.fixedDeltaTime;
            if (self.EngineOn())
                flight.Capacitor = Mathf.Min(WarewindConstants.CapacitorMax, flight.Capacitor + WarewindConstants.CapacitorRegenPerS * dt);

            if (!WarewindThreatScan.TryFind(self, out Missile threat, out WarewindThreatKind kind))
                return;

            if (kind == WarewindThreatKind.Ir)
            {
                WarewindFlares.DumpOnThreat(self, flight);
                return;
            }

            Transform? antenna = flight.EwDummy;
            Vector3 to = threat.transform.position - (antenna != null ? antenna.position : self.transform.position);
            if (antenna != null && to.sqrMagnitude > 0.01f)
                SlewAntenna(antenna, to, dt);

            float dist = to.magnitude;
            float fall = _falloff != null
                ? Mathf.Clamp01(_falloff.Evaluate(dist))
                : 1f / (1f + dist * 0.0001f);
            float powerScale = Mathf.Clamp(_refPower / 800f, 0.85f, 1.6f);
            float frameJam = WarewindConstants.JamPerSecond * dt * fall * powerScale;
            float draw = frameJam * WarewindConstants.JamDrawPerS;
            if (flight.Capacitor < draw)
                return;
            flight.Capacitor -= draw;

            WarewindJamInject.Pulse(threat, self, frameJam);

            if (Time.timeSinceLevelLoad - _lastLog > 2f)
            {
                _lastLog = Time.timeSinceLevelLoad;
                WarewindPlugin.ModLog?.LogInfo(
                    $"Warewind jam {threat.name} dist={dist:F0}m j/s={frameJam / dt:F2} cap={flight.Capacitor:F0}");
            }
        }

        private static void SlewAntenna(Transform antenna, Vector3 toThreat, float dt)
        {
            if (toThreat.sqrMagnitude < 0.01f)
                return;
            Quaternion want = Quaternion.LookRotation(toThreat);
            float maxDeg = WarewindConstants.JamAntennaSlewDegS * dt;
            antenna.rotation = maxDeg >= 179f
                ? want
                : Quaternion.RotateTowards(antenna.rotation, want, maxDeg);
        }

        private static bool NameInList(string name, string[] aliases)
        {
            for (int i = 0; i < aliases.Length; i++)
            {
                if (string.Equals(name, aliases[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
