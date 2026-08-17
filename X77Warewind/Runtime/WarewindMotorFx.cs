using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Exhaust-only FX on PlaceOfSpawnEngine*. No TrailEmitter (AAM vapor C-puffs),
    /// no cone/shock/mesh wedges. Stock PS under hidden AAM2 mesh are silenced.
    /// </summary>
    internal static class WarewindMotorFx
    {
        private static readonly FieldInfo? MotorsField =
            typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type? MotorType =
            typeof(Missile).GetNestedType("Motor", BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly FieldInfo? ParticlesField =
            MotorType?.GetField("particleSystems", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? TrailsField =
            MotorType?.GetField("trailEmitters", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? LightsField =
            MotorType?.GetField("lights", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? AudioField =
            MotorType?.GetField("audioSources", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo? StartupField =
            MotorType?.GetField("startupSource", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly string[] JunkNameParts =
        {
            "cone", "shock", "vapor", "contrail", "wing", "fin", "radar", "seeker",
            "bloom", "flare", "glow", "trail", "smoke", "mist", "cloud", "shockwave"
        };
        private static readonly string[] ExhaustNameParts =
        {
            "exhaust", "thrust", "flame", "fire", "engine", "motor", "plume", "nozzle"
        };

        private static readonly List<GameObject> TbmPsTemplates = new List<GameObject>(4);
        private static readonly List<AudioClip> TbmAudioClips = new List<AudioClip>(2);
        private static GameObject? _tbmHold;

        /// <summary>Cache Piledriver / BallisticMissile1 booster exhaust for stage-1 FX.</summary>
        internal static void CaptureTbm(Encyclopedia enc)
        {
            TbmPsTemplates.Clear();
            TbmAudioClips.Clear();
            if (enc == null || MotorsField == null || ParticlesField == null)
                return;

            MissileDefinition? tbm = FindTbmDefinition(enc);
            if (tbm?.unitPrefab == null)
            {
                WarewindPlugin.ModLog?.LogWarning("Warewind: no BallisticMissile1 for TBM FX.");
                return;
            }

            Missile? mis = tbm.unitPrefab.GetComponent<Missile>();
            if (mis == null)
                mis = tbm.unitPrefab.GetComponentInChildren<Missile>(true);
            if (mis == null)
                return;

            Array? motors = MotorsField.GetValue(mis) as Array;
            if (motors == null || motors.Length == 0)
                return;

            object? booster = motors.GetValue(0);
            if (booster == null)
                return;

            if (_tbmHold == null)
            {
                _tbmHold = new GameObject("Warewind_TbmFxHold");
                UnityEngine.Object.DontDestroyOnLoad(_tbmHold);
                _tbmHold.SetActive(false);
            }

            if (ParticlesField.GetValue(booster) is Array psArr)
            {
                ParticleSystem? best = null;
                float bestScore = -1f;
                for (int i = 0; i < psArr.Length; i++)
                {
                    if (psArr.GetValue(i) is not ParticleSystem ps || ps == null)
                        continue;
                    float score = ExhaustScoreTbm(ps);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = ps;
                    }
                }
                if (best != null)
                {
                    GameObject go = UnityEngine.Object.Instantiate(best.gameObject, _tbmHold.transform);
                    go.name = "TbmExhaustTpl";
                    go.SetActive(false);
                    TbmPsTemplates.Add(go);
                }
            }

            if (AudioField?.GetValue(booster) is Array aud && aud.Length > 0)
            {
                for (int i = 0; i < aud.Length && i < 2; i++)
                {
                    if (aud.GetValue(i) is AudioSource a && a != null && a.clip != null)
                        TbmAudioClips.Add(a.clip);
                }
            }

            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind TBM FX captured from '{tbm.jsonKey}' ps={TbmPsTemplates.Count} audio={TbmAudioClips.Count}");
        }

        private static MissileDefinition? FindTbmDefinition(Encyclopedia enc)
        {
            if (enc.missiles == null)
                return null;
            MissileDefinition? best = null;
            int score = -1;
            for (int i = 0; i < enc.missiles.Count; i++)
            {
                MissileDefinition? m = enc.missiles[i];
                if (m == null || string.IsNullOrEmpty(m.jsonKey) || m.unitPrefab == null)
                    continue;
                string k = m.jsonKey;
                int s = 0;
                if (k.Equals("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
                    s = 100;
                else if (k.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase))
                    s = 80;
                else if (k.IndexOf("BallisticMissile", StringComparison.OrdinalIgnoreCase) >= 0)
                    s = 40;
                if (s > score)
                {
                    score = s;
                    best = m;
                }
            }
            return best;
        }

        private static float ExhaustScoreTbm(ParticleSystem ps)
        {
            string n = (ps.gameObject.name ?? string.Empty).ToLowerInvariant();
            // TBM solid plume can be named smoke/exhaust — only reject cone/shock wedges.
            if (n.Contains("cone") || n.Contains("shock") || n.Contains("vapor") || n.Contains("wing"))
                return -1f;
            float score = 1f;
            for (int i = 0; i < ExhaustNameParts.Length; i++)
            {
                if (n.Contains(ExhaustNameParts[i]))
                    score += 5f;
            }
            if (n.Contains("smoke") || n.Contains("trail"))
                score += 2f;
            return score;
        }

        internal static void Bind(Missile missile, WarewindFlight flight)
        {
            if (missile == null || flight == null || MotorsField == null || MotorType == null)
                return;

            Transform? sock0 = flight.Engine1;
            Transform? sock1 = flight.Engine2 != null ? flight.Engine2 : flight.Engine1;
            if (sock0 == null && sock1 == null)
            {
                WarewindPlugin.ModLog?.LogWarning("Warewind: no engine FX sockets.");
                return;
            }

            Array? motors = MotorsField.GetValue(missile) as Array;
            if (motors == null || motors.Length == 0)
                return;

            WipeStockFx(missile, sock0, sock1);
            StripAllTrails(motors);
            ClearLights(motors);
            FilterMotorParticles(motors);

            int moved = 0;
            if (motors.Length > 0 && motors.GetValue(0) is object m0)
            {
                if (TbmPsTemplates.Count > 0)
                    moved += InjectTbmBooster(missile, m0, sock0 != null ? sock0 : sock1!);
                else
                    moved += RetargetMotor(missile, m0, sock0 != null ? sock0 : sock1!);
            }
            if (motors.Length > 1 && motors.GetValue(1) is object m1)
                moved += RetargetMotor(missile, m1, sock1 != null ? sock1 : sock0!);

            StripAllTrails(motors);
            WipeStockFx(missile, sock0, sock1);

            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind motor FX sock0={(sock0 != null ? sock0.name : "-")} sock1={(sock1 != null ? sock1.name : "-")} moved={moved} tbm={(TbmPsTemplates.Count > 0)}");
        }

        private static int InjectTbmBooster(Missile missile, object motor, Transform socket)
        {
            if (ParticlesField == null || TbmPsTemplates.Count == 0)
                return 0;

            // Silence whatever AAM left on this motor slot.
            if (ParticlesField.GetValue(motor) is Array old)
            {
                for (int i = 0; i < old.Length; i++)
                {
                    if (old.GetValue(i) is ParticleSystem ps)
                        SilenceStock(ps);
                }
            }

            GameObject tpl = TbmPsTemplates[0];
            GameObject go = UnityEngine.Object.Instantiate(tpl);
            go.name = "WarewindTbmExhaust";
            PlaceOnSocket(go.transform, socket, missile);
            go.SetActive(true);

            foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr != null)
                    UnityEngine.Object.DestroyImmediate(mr.gameObject);
            }
            foreach (Light lit in go.GetComponentsInChildren<Light>(true))
            {
                if (lit != null)
                    UnityEngine.Object.DestroyImmediate(lit);
            }
            foreach (TrailEmitter te in go.GetComponentsInChildren<TrailEmitter>(true))
            {
                if (te != null)
                    UnityEngine.Object.DestroyImmediate(te);
            }

            ParticleSystem? root = go.GetComponent<ParticleSystem>();
            if (root == null)
                root = go.GetComponentInChildren<ParticleSystem>(true);
            if (root == null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                ParticlesField.SetValue(motor, Array.CreateInstance(typeof(ParticleSystem), 0));
                return 0;
            }

            foreach (ParticleSystem child in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (child == null)
                    continue;
                MakeLoopingExhaust(child);
                CapStartSize(child);
            }
            foreach (ParticleSystemRenderer r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r != null)
                    r.enabled = true;
            }

            ParticlesField.SetValue(motor, new ParticleSystem[] { root });

            // One-shot burn audio — loop=true made TBM ignition/rumble restart forever.
            if (AudioField != null && TbmAudioClips.Count > 0)
            {
                AudioSource[] srcs = new AudioSource[1];
                GameObject ago = new GameObject("WarewindTbmAudio");
                PlaceOnSocket(ago.transform, socket, missile);
                AudioSource a = ago.AddComponent<AudioSource>();
                a.clip = TbmAudioClips[0];
                a.playOnAwake = false;
                a.loop = false;
                a.spatialBlend = 1f;
                a.minDistance = 20f;
                a.maxDistance = 2000f;
                srcs[0] = a;
                AudioField.SetValue(motor, srcs);
            }

            // delayTimer path re-Play()s startup whenever !isPlaying → cyclic click/whoosh.
            StartupField?.SetValue(motor, null);
            return 1;
        }

        /// <summary>Stop stage-0 FX/audio when booster is spent / separated.</summary>
        internal static void StopStage(Missile missile, int stage)
        {
            if (missile == null || MotorsField == null || stage < 0)
                return;
            Array? motors = MotorsField.GetValue(missile) as Array;
            if (motors == null || stage >= motors.Length || motors.GetValue(stage) is not object motor)
                return;

            if (ParticlesField?.GetValue(motor) is Array psArr)
            {
                for (int i = 0; i < psArr.Length; i++)
                {
                    if (psArr.GetValue(i) is ParticleSystem ps && ps != null)
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            if (AudioField?.GetValue(motor) is Array aud)
            {
                for (int i = 0; i < aud.Length; i++)
                {
                    if (aud.GetValue(i) is AudioSource a && a != null)
                    {
                        a.loop = false;
                        a.Stop();
                    }
                }
            }
            StartupField?.SetValue(motor, null);
        }

        internal static void KeepAlive(Missile missile)
        {
            if (missile == null || MotorsField == null || ParticlesField == null)
                return;
            Array? motors = MotorsField.GetValue(missile) as Array;
            if (motors == null || motors.Length == 0)
                return;

            int stage = WarewindMotors.MotorStage(missile);
            if (stage < 0 || stage >= motors.Length)
                return;
            if (motors.GetValue(stage) is not object motor)
                return;
            if (ReadMotorFuel(motor) <= 0.05f)
                return;

            if (ParticlesField.GetValue(motor) is Array psArr)
            {
                for (int i = 0; i < psArr.Length; i++)
                {
                    if (psArr.GetValue(i) is ParticleSystem ps && ps != null && !ps.isPlaying)
                        ps.Play(true);
                }
            }
        }

        private static void WipeStockFx(Missile missile, Transform? sock0, Transform? sock1)
        {
            TrailEmitter[] trails = missile.GetComponentsInChildren<TrailEmitter>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                TrailEmitter te = trails[i];
                if (te == null)
                    continue;
                if (IsUnder(te.transform, sock0) || IsUnder(te.transform, sock1))
                    continue;
                te.StopTrail();
                te.enabled = false;
                UnityEngine.Object.Destroy(te);
            }

            ParticleSystem[] all = missile.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < all.Length; i++)
            {
                ParticleSystem ps = all[i];
                if (ps == null)
                    continue;
                if (IsUnder(ps.transform, sock0) || IsUnder(ps.transform, sock1))
                    continue;
                // Keep only our _ww clones under sockets; silence everything else (AAM mesh FX).
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
        }

        private static bool IsUnder(Transform t, Transform? sock)
        {
            if (sock == null || t == null)
                return false;
            return t == sock || t.IsChildOf(sock);
        }

        private static void StripAllTrails(Array motors)
        {
            if (TrailsField == null)
                return;
            Array empty = Array.CreateInstance(typeof(TrailEmitter), 0);
            for (int i = 0; i < motors.Length; i++)
            {
                if (motors.GetValue(i) is not object motor)
                    continue;
                if (TrailsField.GetValue(motor) is Array old)
                {
                    for (int j = 0; j < old.Length; j++)
                    {
                        if (old.GetValue(j) is TrailEmitter te && te != null)
                        {
                            te.StopTrail();
                            te.enabled = false;
                        }
                    }
                }
                TrailsField.SetValue(motor, empty);
            }
        }

        private static void FilterMotorParticles(Array motors)
        {
            if (ParticlesField == null)
                return;

            for (int mi = 0; mi < motors.Length; mi++)
            {
                if (motors.GetValue(mi) is not object motor)
                    continue;
                if (ParticlesField.GetValue(motor) is not Array src || src.Length == 0)
                {
                    ParticlesField.SetValue(motor, Array.CreateInstance(typeof(ParticleSystem), 0));
                    continue;
                }

                ParticleSystem? best = null;
                float bestScore = -1f;
                for (int i = 0; i < src.Length; i++)
                {
                    if (src.GetValue(i) is not ParticleSystem ps || ps == null)
                        continue;
                    float score = ExhaustScore(ps);
                    if (score < 0f)
                    {
                        SilenceStock(ps);
                        continue;
                    }
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = ps;
                    }
                }

                if (best == null)
                {
                    ParticlesField.SetValue(motor, Array.CreateInstance(typeof(ParticleSystem), 0));
                    continue;
                }

                // Silence losers; keep one winner for clone.
                for (int i = 0; i < src.Length; i++)
                {
                    if (src.GetValue(i) is ParticleSystem ps && ps != null && ps != best)
                        SilenceStock(ps);
                }
                ParticlesField.SetValue(motor, new ParticleSystem[] { best });
            }
        }

        private static float ExhaustScore(ParticleSystem ps)
        {
            string n = (ps.gameObject.name ?? string.Empty).ToLowerInvariant();
            for (int i = 0; i < JunkNameParts.Length; i++)
            {
                if (n.Contains(JunkNameParts[i]))
                    return -1f;
            }

            ParticleSystem.MainModule main = ps.main;
            float size = main.startSize.mode == ParticleSystemCurveMode.TwoConstants
                ? Mathf.Max(main.startSize.constantMin, main.startSize.constantMax)
                : main.startSize.constant;
            if (size > WarewindConstants.FxMaxStartSize * 4f)
                return -1f;

            ParticleSystem.ShapeModule shape = ps.shape;
            if (shape.enabled &&
                (shape.shapeType == ParticleSystemShapeType.Cone ||
                 shape.shapeType == ParticleSystemShapeType.ConeVolume ||
                 shape.shapeType == ParticleSystemShapeType.Box ||
                 shape.shapeType == ParticleSystemShapeType.Hemisphere) &&
                shape.radius > 2f)
                return -1f;

            float score = 1f;
            for (int i = 0; i < ExhaustNameParts.Length; i++)
            {
                if (n.Contains(ExhaustNameParts[i]))
                    score += 5f;
            }
            // Prefer small flame PS.
            score += Mathf.Clamp(3f - size, 0f, 3f);
            return score;
        }

        private static float ReadMotorFuel(object motor)
        {
            FieldInfo? f = MotorType?.GetField("fuelMass", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null)
                return 0f;
            return f.GetValue(motor) is float n ? n : 0f;
        }

        private static void ClearLights(Array motors)
        {
            if (LightsField == null)
                return;
            for (int i = 0; i < motors.Length; i++)
            {
                if (motors.GetValue(i) is not object m)
                    continue;
                LightsField.SetValue(m, Array.CreateInstance(typeof(Light), 0));
            }
        }

        private static int RetargetMotor(Missile missile, object motor, Transform socket)
        {
            int n = 0;
            n += MoveParticles(missile, motor, socket);
            n += MoveAudio(missile, motor, socket);

            if (StartupField != null && StartupField.GetValue(motor) is AudioSource startup && startup != null)
            {
                // Null after optional one Play via Activate — delayTimer would re-trigger forever.
                StartupField.SetValue(motor, null);
            }
            return n;
        }

        private static int MoveParticles(Missile missile, object motor, Transform socket)
        {
            if (ParticlesField == null)
                return 0;
            Array? src = ParticlesField.GetValue(motor) as Array;
            if (src == null || src.Length == 0)
                return 0;

            ParticleSystem? copy = null;
            for (int i = 0; i < src.Length; i++)
            {
                if (src.GetValue(i) is not ParticleSystem ps || ps == null)
                    continue;
                copy = CloneParticle(ps, socket, missile);
                SilenceStock(ps);
                break;
            }

            if (copy == null)
            {
                ParticlesField.SetValue(motor, Array.CreateInstance(typeof(ParticleSystem), 0));
                return 0;
            }
            ParticlesField.SetValue(motor, new ParticleSystem[] { copy });
            return 1;
        }

        private static int MoveAudio(Missile missile, object motor, Transform socket)
        {
            if (AudioField == null)
                return 0;
            Array? src = AudioField.GetValue(motor) as Array;
            if (src == null || src.Length == 0)
                return 0;

            Array dst = Array.CreateInstance(typeof(AudioSource), src.Length);
            int n = 0;
            for (int i = 0; i < src.Length; i++)
            {
                if (src.GetValue(i) is not AudioSource a || a == null)
                    continue;
                AudioSource? c = CloneAudio(a, socket) as AudioSource;
                if (c == null)
                    continue;
                dst.SetValue(c, i);
                n++;
                a.Stop();
            }
            AudioField.SetValue(motor, dst);
            return n;
        }

        private static ParticleSystem? CloneParticle(ParticleSystem ps, Transform socket, Missile missile)
        {
            if (ExhaustScore(ps) < 0f)
                return null;

            GameObject go = UnityEngine.Object.Instantiate(ps.gameObject);
            go.name = "WarewindExhaust";
            PlaceOnSocket(go.transform, socket, missile);
            go.SetActive(true);

            // Destroy mesh wedges / lights / nested junk PS.
            foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr != null)
                    UnityEngine.Object.DestroyImmediate(mr.gameObject);
            }
            foreach (Light lit in go.GetComponentsInChildren<Light>(true))
            {
                if (lit != null)
                    UnityEngine.Object.DestroyImmediate(lit);
            }
            foreach (TrailEmitter te in go.GetComponentsInChildren<TrailEmitter>(true))
            {
                if (te != null)
                    UnityEngine.Object.DestroyImmediate(te);
            }

            ParticleSystem[] kids = go.GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystem? root = go.GetComponent<ParticleSystem>();
            for (int i = 0; i < kids.Length; i++)
            {
                ParticleSystem child = kids[i];
                if (child == null)
                    continue;
                if (child != root && ExhaustScore(child) < 0f)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    continue;
                }
                MakeLoopingExhaust(child);
                CapStartSize(child);
            }

            root = go.GetComponent<ParticleSystem>();
            if (root == null)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return null;
            }

            foreach (ParticleSystemRenderer r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (r != null)
                    r.enabled = true;
            }
            return root;
        }

        private static void MakeLoopingExhaust(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (main.duration < 1f)
                main.duration = 5f;
            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = true;
            ParticleSystem.ShapeModule shape = ps.shape;
            if (shape.enabled && shape.radius > 0.8f)
                shape.radius = 0.25f;
        }

        private static void CapStartSize(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            if (main.startSize.mode == ParticleSystemCurveMode.TwoConstants)
            {
                float a = Mathf.Min(main.startSize.constantMin, WarewindConstants.FxMaxStartSize);
                float b = Mathf.Min(main.startSize.constantMax, WarewindConstants.FxMaxStartSize);
                main.startSize = new ParticleSystem.MinMaxCurve(a, b);
            }
            else
            {
                float s = Mathf.Min(main.startSize.constant, WarewindConstants.FxMaxStartSize);
                main.startSize = s;
            }
        }

        private static Component? CloneAudio(AudioSource src, Transform socket)
        {
            if (src == null)
                return null;
            Missile? m = socket.GetComponentInParent<Missile>();
            if (m == null)
                return null;
            GameObject go = new GameObject(src.gameObject.name + "_ww");
            PlaceOnSocket(go.transform, socket, m);

            AudioSource dst = go.AddComponent<AudioSource>();
            dst.clip = src.clip;
            dst.outputAudioMixerGroup = src.outputAudioMixerGroup;
            dst.playOnAwake = false;
            // Never loop motor one-shots — cyclic restart sounds broken.
            dst.loop = false;
            dst.volume = src.volume;
            dst.pitch = src.pitch;
            dst.spatialBlend = src.spatialBlend;
            dst.minDistance = Mathf.Max(src.minDistance, 15f);
            dst.maxDistance = Mathf.Max(src.maxDistance, 1500f);
            dst.dopplerLevel = src.dopplerLevel;
            dst.bypassListenerEffects = src.bypassListenerEffects;
            return dst;
        }

        private static void PlaceOnSocket(Transform t, Transform socket, Missile missile)
        {
            float parent = Mathf.Max(0.01f, (socket.lossyScale.x + socket.lossyScale.y + socket.lossyScale.z) / 3f);
            float local = WarewindConstants.FxWorldScaleM / parent;
            t.SetParent(socket, false);
            t.localPosition = Vector3.zero;
            t.localScale = new Vector3(local, local, local);
            Vector3 aft = -missile.transform.forward;
            if (aft.sqrMagnitude < 0.01f)
                aft = -socket.forward;
            t.rotation = Quaternion.LookRotation(aft, missile.transform.up);
            t.position = socket.position + aft * WarewindConstants.FxAftNudgeM;
        }

        private static void SilenceStock(Component c)
        {
            if (c == null)
                return;
            if (c is ParticleSystem ps)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
            if (c is Light lit)
                lit.enabled = false;
            if (c is AudioSource a)
                a.Stop();
        }
    }
}
