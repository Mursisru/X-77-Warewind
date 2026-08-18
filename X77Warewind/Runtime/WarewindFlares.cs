using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>Own flare dump — vanilla IRFlare prefab, IR on this missile (not aircraft CM).</summary>
    internal static class WarewindFlares
    {
        private static readonly FieldInfo? AircraftField =
            typeof(IRFlare).GetField("aircraft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? VelocityField =
            typeof(IRFlare).GetField("velocity", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? IrField =
            typeof(IRFlare).GetField("IR", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? NearField =
            typeof(IRFlare).GetField("nearAircraft", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? FlarePrefabField =
            typeof(FlareEjector).GetField("flarePrefab", BindingFlags.Instance | BindingFlags.NonPublic);

        private static GameObject? _prefab;

        internal static void Cache(Encyclopedia enc)
        {
            if (_prefab != null || enc?.aircraft == null)
                return;
            foreach (AircraftDefinition ad in enc.aircraft)
            {
                if (ad?.unitPrefab == null)
                    continue;
                FlareEjector[] ejectors = ad.unitPrefab.GetComponentsInChildren<FlareEjector>(true);
                for (int i = 0; i < ejectors.Length; i++)
                {
                    if (ejectors[i] == null)
                        continue;
                    GameObject? go = FlarePrefabField?.GetValue(ejectors[i]) as GameObject;
                    if (go == null)
                        continue;
                    _prefab = go;
                    WarewindPlugin.ModLog?.LogInfo($"Warewind flare prefab from '{ad.jsonKey}'");
                    return;
                }
            }
        }

        internal static void DumpOnThreat(Missile missile, WarewindFlight flight, float distTarget)
        {
            if (missile == null || flight == null || _prefab == null)
                return;
            if (distTarget > WarewindConstants.FlareRangeM)
                return;
            if (flight.FlaresLeft <= 0)
                return;
            if (Time.timeSinceLevelLoad - flight.LastFlareTime < WarewindConstants.FlareIntervalS * 0.5f)
                return;

            flight.LastFlareTime = Time.timeSinceLevelLoad;
            Transform socket = PickSocket(flight, missile.transform);
            Vector3 dir = socket.forward;
            if (dir.sqrMagnitude < 0.01f)
                dir = -missile.transform.forward;
            Vector3 vel = (missile.rb != null ? missile.rb.velocity : Vector3.zero) +
                          dir.normalized * WarewindConstants.FlareEjectSpeed;
            Spawn(missile, socket.position, vel);
            flight.FlaresLeft--;
        }

        internal static void Tick(Missile missile, WarewindFlight flight, float distTarget)
        {
            if (missile == null || flight == null || _prefab == null)
                return;
            if (flight.FlaresLeft <= 0)
                return;
            if (distTarget > WarewindConstants.FlareRangeM)
                return;
            if (Time.timeSinceLevelLoad - flight.LastFlareTime < WarewindConstants.FlareIntervalS)
                return;

            flight.LastFlareTime = Time.timeSinceLevelLoad;
            Transform socket = PickSocket(flight, missile.transform);
            Vector3 dir = socket.forward;
            if (dir.sqrMagnitude < 0.01f)
                dir = -missile.transform.forward;
            Vector3 vel = (missile.rb != null ? missile.rb.velocity : Vector3.zero) +
                          dir.normalized * WarewindConstants.FlareEjectSpeed;
            Spawn(missile, socket.position, vel);
            flight.FlaresLeft--;
        }

        private static Transform PickSocket(WarewindFlight flight, Transform fallback)
        {
            List<Transform> list = flight.FlareSockets;
            if (list.Count == 0)
                return fallback;
            int i = (WarewindConstants.FlareCount - flight.FlaresLeft) % list.Count;
            Transform t = list[i];
            return t != null ? t : fallback;
        }

        private static void Spawn(Missile missile, Vector3 pos, Vector3 vel)
        {
            GameObject? prefab = _prefab;
            if (prefab == null)
                return;
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.LookRotation(vel.sqrMagnitude > 0.01f ? vel : Vector3.up));
            IRFlare? flare = go.GetComponent<IRFlare>() ?? go.GetComponentInChildren<IRFlare>(true);
            if (flare == null)
            {
                Object.Destroy(go);
                return;
            }

            go.SetActive(true);
            IRSource ir = new IRSource(flare.transform, 1f, true);
            IrField?.SetValue(flare, ir);
            VelocityField?.SetValue(flare, vel);
            AircraftField?.SetValue(flare, null);
            NearField?.SetValue(flare, false);
            missile.AddIRSource(ir);
        }
    }
}
