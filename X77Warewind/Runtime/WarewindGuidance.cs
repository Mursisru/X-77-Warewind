using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// AimPoint-only guidance (vanilla Steering + thrust along nose).
    /// No velocity rewrite. Echelon is a loft hint — dive opens by range, not by reaching cruise.
    /// </summary>
    internal static class WarewindGuidance
    {
        internal static void Tick(Missile missile)
        {
            if (missile == null || missile.disabled)
                return;
            WarewindFlight? f = missile.GetComponent<WarewindFlight>();
            if (f == null)
                return;

            f.SyncTarget();
            Vector3 pos = missile.transform.position;
            Vector3 tgt = f.LastKnownPos;
            WarewindProfile.Ensure(f, f.LaunchPos.sqrMagnitude > 1f ? f.LaunchPos : pos, tgt);

            float dist = Horiz(pos, tgt);
            float alt = pos.y - Datum.LocalSeaY;
            float climb = missile.rb != null ? missile.rb.velocity.y : 0f;
            float speed = missile.rb != null ? missile.rb.velocity.magnitude : 0f;
            float t = missile.timeSinceSpawn;

            WarewindDockEject.TryEject(missile, f);
            AdvancePhase(f, dist, alt, t);
            WarewindStageSep.TrySeparate(missile, f);
            WarewindAero.Tick(missile);
            WarewindMotorFx.KeepAlive(missile);
            ApplyThrottle(missile, f, alt, climb, speed);
            WarewindMotors.ClampSpeed(missile);

            // Pure AimPoint — geometric point ahead; Steering does the rest.
            GlobalPosition aim = BuildAimpoint(missile, f, pos, tgt, dist, alt, climb);
            missile.SetAimpoint(aim, f.LastKnownVel);

            if (!f.Armed && t >= WarewindConstants.ArmAfterS)
            {
                WarewindFuse.ArmNow(missile);
                f.Armed = true;
                WarewindShellPrep.EnableImpact(missile);
            }
            if (!missile.IsTangible() && t >= WarewindConstants.TangibleAfterS)
                missile.SetTangible(true);
            if (!f.FinsOut && f.Phase != WarewindPhase.Drop)
            {
                missile.DeployFins();
                f.FinsOut = true;
                WarewindAero.OnFinsDeployed(missile);
            }

            WarewindFlares.Tick(missile, f, dist);
            WarewindEw.Tick(missile, f);

            if (t > WarewindConstants.SoftKillTimeoutS)
                WarewindFuse.DetonateNow(missile);
        }

        private static void AdvancePhase(WarewindFlight f, float dist, float alt, float t)
        {
            // Dive by proximity from ANY mid-course phase — echelon is never a gate.
            if (f.Phase is WarewindPhase.Align or WarewindPhase.Loft or WarewindPhase.Cruise
                && dist <= f.DiveCommitDistM)
            {
                f.Phase = WarewindPhase.Dive;
                return;
            }

            switch (f.Phase)
            {
                case WarewindPhase.Drop:
                {
                    float fallen = f.LaunchY - f.transform.position.y;
                    if (t >= WarewindConstants.StabilizeSeconds &&
                        (t >= WarewindConstants.MotorDelayS || fallen >= WarewindConstants.DropFallM))
                        f.Phase = WarewindPhase.Align;
                    break;
                }
                case WarewindPhase.Align:
                    if (t >= WarewindConstants.MotorDelayS + WarewindConstants.AlignPhaseS)
                        f.Phase = f.ShallowLoft ? WarewindPhase.Cruise : WarewindPhase.Loft;
                    break;
                case WarewindPhase.Loft:
                    if (alt >= f.LoftEnterAltM || alt >= f.CruiseAltM)
                        f.Phase = WarewindPhase.Cruise;
                    break;
            }
        }

        private static void ApplyThrottle(
            Missile missile, WarewindFlight f, float alt, float climb, float speed)
        {
            if (f.Phase == WarewindPhase.Drop)
            {
                missile.SetThrottle(0f);
                return;
            }

            float top = WarewindMotors.StageTopSpeed(missile);
            if (speed >= top * 0.99f)
            {
                missile.SetThrottle(0f);
                return;
            }

            // Soft hold only — never dump energy "backwards".
            if (alt > f.CruiseAltM + 1500f && climb > 30f && f.Phase != WarewindPhase.Dive)
            {
                missile.SetThrottle(WarewindConstants.PartialThrottle * 0.5f);
                return;
            }

            if (f.Phase == WarewindPhase.Align)
                missile.SetThrottle(WarewindConstants.PartialThrottle);
            else if (f.Phase == WarewindPhase.Cruise)
                missile.SetThrottle(WarewindConstants.CruiseThrottle);
            else
                missile.SetThrottle(WarewindConstants.FullThrottle);
        }

        /// <summary>Where the seeker should look — only AimPoint, no velocity edits.</summary>
        private static GlobalPosition BuildAimpoint(
            Missile missile, WarewindFlight f, Vector3 pos, Vector3 tgt, float dist, float alt, float climb)
        {
            Vector3 toTgt = HorizDir(pos, tgt);
            float cruise = f.CruiseAltM;

            switch (f.Phase)
            {
                case WarewindPhase.Drop:
                {
                    Vector3 p = pos + PitchToward(toTgt, WarewindConstants.DropPitchDeg) * WarewindConstants.AimLookaheadM;
                    return p.ToGlobalPosition();
                }
                case WarewindPhase.Align:
                case WarewindPhase.Loft:
                {
                    float pitch = LoftPitchDeg(f, alt, climb, cruise);
                    if (f.ShallowLoft || dist < f.DiveCommitDistM * 1.5f)
                    {
                        Vector3 mid = Vector3.Lerp(pos, tgt, 0.55f);
                        mid.y = Mathf.Max(mid.y, Datum.LocalSeaY + Mathf.Min(cruise, alt + 800f));
                        return mid.ToGlobalPosition();
                    }
                    Vector3 loftPt = pos + toTgt * WarewindConstants.AimLookaheadM;
                    loftPt.y = Datum.LocalSeaY + cruise;
                    Vector3 pitched = pos + PitchToward(toTgt, pitch) * WarewindConstants.AimLookaheadM;
                    Vector3 blend = Vector3.Lerp(pitched, loftPt, 0.55f);
                    return blend.ToGlobalPosition();
                }
                case WarewindPhase.Cruise:
                {
                    Vector3 h = toTgt;
                    if (Time.timeSinceLevelLoad - f.LastSamTime >= WarewindConstants.SamRefreshS)
                    {
                        f.LastSamTime = Time.timeSinceLevelLoad;
                        Vector3 probe = pos + toTgt * WarewindConstants.AimLookaheadM;
                        probe.y = Datum.LocalSeaY + cruise;
                        f.SamAim = WarewindSamAvoid.OffsetCruise(pos, probe, missile);
                    }
                    if (f.SamAim.sqrMagnitude > 1f)
                    {
                        Vector3 samH = HorizDir(pos, f.SamAim);
                        if (Vector3.Dot(samH, toTgt) > 0.2f)
                            h = samH;
                    }
                    Vector3 cruisePt = pos + h * WarewindConstants.AimLookaheadM;
                    cruisePt.y = Datum.LocalSeaY + cruise;
                    if (dist < f.DiveCommitDistM * 2f)
                        cruisePt = Vector3.Lerp(cruisePt, tgt, 1f - dist / (f.DiveCommitDistM * 2f));
                    return cruisePt.ToGlobalPosition();
                }
                default:
                {
                    if (dist <= WarewindConstants.TerminalDirectDistM)
                    {
                        Vector3 lead = tgt + f.LastKnownVel * 0.35f;
                        return lead.ToGlobalPosition();
                    }
                    Vector3 dive = tgt;
                    if (dist > 12000f)
                        dive.y = Mathf.Max(tgt.y, Datum.LocalSeaY + Mathf.Min(alt * 0.35f, 4000f));
                    return dive.ToGlobalPosition();
                }
            }
        }

        private static float LoftPitchDeg(WarewindFlight f, float alt, float climb, float cruise)
        {
            float maxPitch = f.ShallowLoft
                ? WarewindConstants.LoftPitchShallowDeg
                : WarewindConstants.LoftPitchMaxDeg;
            float remain = cruise - alt;
            float pitch;
            if (remain > Mathf.Max(2000f, cruise * 0.4f))
                pitch = maxPitch;
            else if (remain > 500f)
                pitch = Mathf.Lerp(maxPitch, WarewindConstants.LoftPitchMinDeg,
                    1f - remain / Mathf.Max(500f, cruise * 0.4f));
            else if (remain > 0f)
                pitch = WarewindConstants.LoftPitchMinDeg;
            else
                pitch = WarewindConstants.OvershootPitchDeg;

            float predicted = alt + climb * WarewindConstants.ClimbPredictS;
            if (predicted > cruise)
                pitch = Mathf.Min(pitch, WarewindConstants.OvershootPitchDeg * 0.5f);
            return Mathf.Clamp(pitch, WarewindConstants.OvershootPitchDeg, maxPitch);
        }

        private static Vector3 PitchToward(Vector3 horizDir, float pitchDeg)
        {
            horizDir = Flat(horizDir);
            Vector3 right = Vector3.Cross(Vector3.up, horizDir);
            if (right.sqrMagnitude < 0.01f)
                right = Vector3.right;
            right.Normalize();
            return (Quaternion.AngleAxis(-pitchDeg, right) * horizDir).normalized;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.01f ? v.normalized : Vector3.forward;
        }

        private static Vector3 HorizDir(Vector3 from, Vector3 to) => Flat(to - from);

        private static float Horiz(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
