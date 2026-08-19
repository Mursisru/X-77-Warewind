using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Motor TrailEmitter from TBM (prefer) / AAM2 motor. Skip wing vapor C-puffs.
    /// Vanilla emitLifetime ~10s — we stretch it so the trail lasts the whole burn.
    /// </summary>
    internal static class WarewindMotorTrails
    {
        private static readonly Type? MotorType =
            typeof(Missile).GetNestedType("Motor", BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo? MotorsField =
            typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? TrailsField =
            MotorType?.GetField("trailEmitters", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ActivatedField =
            MotorType?.GetField("activated", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo? TrailSystemField =
            typeof(TrailEmitter).GetField("trailSystem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? EmitTransformField =
            typeof(TrailEmitter).GetField("emitTransform", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? EmitLifetimeField =
            typeof(TrailEmitter).GetField("emitLifetime", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly string[] JunkNameParts =
        {
            "vapor", "contrail", "cone", "shock", "wing", "fin"
        };

        private static readonly List<GameObject> TbmTemplates = new List<GameObject>(2);
        private static readonly List<GameObject> AamTemplates = new List<GameObject>(2);
        private static GameObject? _hold;

        internal static void CaptureFromMotor(object? motor)
        {
            CaptureInto(motor, TbmTemplates);
            WarewindPlugin.ModLog?.LogInfo($"Warewind TBM trails captured={TbmTemplates.Count}");
        }

        internal static void CaptureAam(Encyclopedia enc)
        {
            if (enc?.missiles == null || MotorsField == null)
                return;

            MissileDefinition? aam = null;
            for (int i = 0; i < enc.missiles.Count; i++)
            {
                MissileDefinition? m = enc.missiles[i];
                if (m == null || m.unitPrefab == null || string.IsNullOrEmpty(m.jsonKey))
                    continue;
                if (m.jsonKey.Equals(WarewindConstants.ShellMissileKey, StringComparison.OrdinalIgnoreCase) ||
                    m.jsonKey.Equals(WarewindConstants.ShellMissileKeyAlt, StringComparison.OrdinalIgnoreCase))
                {
                    aam = m;
                    break;
                }
            }
            if (aam?.unitPrefab == null)
                return;

            Missile? mis = aam.unitPrefab.GetComponent<Missile>();
            if (mis == null)
                mis = aam.unitPrefab.GetComponentInChildren<Missile>(true);
            if (mis == null)
                return;

            Array? motors = MotorsField.GetValue(mis) as Array;
            if (motors == null || motors.Length == 0)
                return;
            CaptureInto(motors.GetValue(0), AamTemplates);
            WarewindPlugin.ModLog?.LogInfo($"Warewind AAM trails captured={AamTemplates.Count}");
        }

        internal static int Bind(Missile missile, Array motors, Transform? sock0, Transform? sock1)
        {
            if (missile == null || motors == null || TrailsField == null)
                return 0;

            List<GameObject> tpl = TbmTemplates.Count > 0 ? TbmTemplates : AamTemplates;
            if (tpl.Count == 0)
            {
                WarewindPlugin.ModLog?.LogWarning("Warewind: no trail templates.");
                return 0;
            }

            int n = 0;
            Transform? s0 = sock0 != null ? sock0 : sock1;
            Transform? s1 = sock1 != null ? sock1 : sock0;
            if (motors.Length > 0 && motors.GetValue(0) is object m0 && s0 != null)
                n += Attach(missile, m0, s0, tpl[0]);
            if (motors.Length > 1 && motors.GetValue(1) is object m1 && s1 != null)
                n += Attach(missile, m1, s1, tpl[0]);
            return n;
        }

        internal static void KeepAlive(object motor, Missile missile)
        {
            if (motor == null || missile == null || TrailsField == null)
                return;
            if (ActivatedField?.GetValue(motor) is bool on && !on)
                return;
            if (TrailsField.GetValue(motor) is not Array arr)
                return;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr.GetValue(i) is not TrailEmitter te || te == null)
                    continue;
                Wire(te, missile);
                if (!te.enabled)
                    te.StartTrail();
            }
        }

        internal static void Stop(object motor)
        {
            if (motor == null || TrailsField == null)
                return;
            if (TrailsField.GetValue(motor) is not Array arr)
                return;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr.GetValue(i) is TrailEmitter te && te != null)
                    te.StopTrail();
            }
        }

        private static void CaptureInto(object? motor, List<GameObject> dst)
        {
            dst.Clear();
            if (motor == null || TrailsField == null)
                return;
            if (TrailsField.GetValue(motor) is not Array src || src.Length == 0)
                return;

            EnsureHold();
            TrailEmitter? best = null;
            float bestScore = -1f;
            for (int i = 0; i < src.Length; i++)
            {
                if (src.GetValue(i) is not TrailEmitter te || te == null)
                    continue;
                float score = Score(te);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = te;
                }
            }
            if (best == null || bestScore < 0f)
            {
                for (int i = 0; i < src.Length; i++)
                {
                    if (src.GetValue(i) is TrailEmitter te && te != null)
                    {
                        best = te;
                        break;
                    }
                }
            }
            if (best == null)
                return;

            GameObject go = UnityEngine.Object.Instantiate(best.gameObject, _hold!.transform);
            go.name = "TrailTpl";
            go.SetActive(false);
            dst.Add(go);
        }

        private static int Attach(Missile missile, object motor, Transform socket, GameObject tpl)
        {
            GameObject go = UnityEngine.Object.Instantiate(tpl);
            go.name = "WarewindTrail";
            WarewindMotorFx.PlaceOnSocket(go.transform, socket, missile);
            go.SetActive(true);

            TrailEmitter? te = go.GetComponent<TrailEmitter>();
            if (te == null)
                te = go.GetComponentInChildren<TrailEmitter>(true);
            if (te == null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                TrailsField!.SetValue(motor, Array.CreateInstance(typeof(TrailEmitter), 0));
                return 0;
            }

            ParticleSystem? ps = TrailSystemField?.GetValue(te) as ParticleSystem;
            if (ps == null)
                ps = go.GetComponent<ParticleSystem>() ?? go.GetComponentInChildren<ParticleSystem>(true);
            if (ps == null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                TrailsField!.SetValue(motor, Array.CreateInstance(typeof(TrailEmitter), 0));
                return 0;
            }

            TrailSystemField?.SetValue(te, ps);
            foreach (ParticleSystemRenderer r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r != null)
                    r.enabled = true;
            }

            Wire(te, missile, go.transform);
            te.enabled = false;
            TrailsField!.SetValue(motor, new TrailEmitter[] { te });
            return 1;
        }

        private static void Wire(TrailEmitter te, Missile missile, Transform? emitAt = null)
        {
            te.rb = missile.rb;
            if (emitAt != null)
                EmitTransformField?.SetValue(te, emitAt);
            else if (EmitTransformField?.GetValue(te) == null)
                EmitTransformField?.SetValue(te, te.transform);
            EmitLifetimeField?.SetValue(te, WarewindConstants.TrailEmitLifetimeS);
        }

        private static float Score(TrailEmitter te)
        {
            string n = (te.gameObject.name ?? string.Empty).ToLowerInvariant();
            for (int i = 0; i < JunkNameParts.Length; i++)
            {
                if (n.Contains(JunkNameParts[i]))
                    return -1f;
            }
            float score = 1f;
            if (n.Contains("trail") || n.Contains("smoke") || n.Contains("exhaust"))
                score += 5f;
            return score;
        }

        private static void EnsureHold()
        {
            if (_hold != null)
                return;
            _hold = new GameObject("Warewind_TrailHold");
            UnityEngine.Object.DontDestroyOnLoad(_hold);
            _hold.SetActive(false);
        }
    }
}
