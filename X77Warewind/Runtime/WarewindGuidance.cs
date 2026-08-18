using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Forward-only aim unless early over-top (tgt astern). Dive by glide geometry, not 35km dump.
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
            float speed = missile.rb != null ? missile.rb.velocity.magnitude : 0f;
            Vector3 vel = missile.rb != null ? missile.rb.velocity : missile.transform.forward;
            float t = missile.timeSinceSpawn;
            bool fuel = WarewindMotors.HasFuel(missile);

            WarewindDockEject.TryEject(missile, f);
            AdvancePhase(f, pos, tgt, dist, alt, t, fuel, vel);
            WarewindStageSep.TrySeparate(missile, f);
            WarewindAero.Tick(missile);
            WarewindMotorFx.KeepAlive(missile);
            ApplyThrottle(missile, f, speed, fuel);
            bool punch = fuel && f.Phase == WarewindPhase.Loft && WarewindMotors.MotorStage(missile) <= 0
                && f.BoosterPunchStartT >= 0f
                && t - f.BoosterPunchStartT <= WarewindConstants.BoosterTwrPunchS;
            WarewindMotors.ApplyBoosterPunch(missile, punch);
            GlobalPosition aim = BuildAimpoint(f, pos, tgt, dist, alt, vel);
            missile.SetAimpoint(aim, f.LastKnownVel);
            WarewindAssist.FollowNose(missile, f, fuel);
            WarewindMotors.ClampSpeed(missile);

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

            WarewindFlares.Tick(missile, f, Vector3.Distance(pos, tgt));
            WarewindEw.Tick(missile, f);

            if (t > WarewindConstants.SoftKillTimeoutS)
                WarewindFuse.DetonateNow(missile);
        }

        private static void AdvancePhase(
            WarewindFlight f, Vector3 pos, Vector3 tgt, float dist, float alt, float t, bool fuel, Vector3 vel)
        {
            if (f.Phase is WarewindPhase.Align or WarewindPhase.Loft or WarewindPhase.Cruise)
            {
                float diveAt = f.DiveCommitDistM;
                if (!fuel)
                    diveAt = Mathf.Max(diveAt, WarewindProfile.GlideDistM(alt, f.DiveAngleMinEff));
                if (dist <= diveAt)
                {
                    f.Phase = WarewindPhase.Dive;
                    return;
                }
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
                {
                    if (t < WarewindConstants.MotorDelayS)
                        break;
                    Vector3 toTgt = HorizDir(pos, tgt);
                    float need = f.DirectAttack
                        ? WarewindConstants.DirectOnCourseDot
                        : WarewindConstants.AlignOnCourseDot;
                    bool onCourse = WarewindLevel.OnCourse(vel, toTgt, need);
                    bool timeout = t >= WarewindConstants.MotorDelayS + WarewindConstants.AlignPhaseMaxS;
                    if (!onCourse && !timeout)
                        break;
                    if (f.DirectAttack)
                        f.Phase = WarewindPhase.Cruise;
                    else if (f.ShallowLoft)
                        f.Phase = WarewindPhase.Cruise;
                    else
                    {
                        f.Phase = WarewindPhase.Loft;
                        f.BoosterPunchStartT = t;
                    }
                    break;
                }
                case WarewindPhase.Loft:
                    if (f.DirectAttack)
                    {
                        f.Phase = WarewindPhase.Cruise;
                        break;
                    }
                    if (alt >= f.LoftEnterAltM || alt >= f.CruiseAltM)
                    {
                        f.Phase = WarewindPhase.Cruise;
                        f.PitchCmd = Mathf.Min(f.PitchCmd, WarewindConstants.CruisePitchMaxDeg);
                    }
                    break;
            }
        }

        private static void ApplyThrottle(Missile missile, WarewindFlight f, float speed, bool fuel)
        {
            if (f.Phase == WarewindPhase.Drop || !fuel)
            {
                missile.SetThrottle(0f);
                return;
            }

            float top = WarewindMotors.StageTopSpeed(missile);
            float alt = missile.transform.position.y - Datum.LocalSeaY;
            bool belowCruise = alt < f.CruiseAltM - 300f;

            if (speed >= top * 0.99f && f.Phase == WarewindPhase.Align)
            {
                missile.SetThrottle(0f);
                return;
            }
            if (speed >= top * 0.99f && f.Phase == WarewindPhase.Loft && belowCruise)
            {
                missile.SetThrottle(WarewindConstants.CruiseThrottle);
                return;
            }
            if (speed >= top * 0.99f && f.Phase == WarewindPhase.Loft)
            {
                missile.SetThrottle(0f);
                return;
            }
            if (speed >= top * 0.99f && f.Phase == WarewindPhase.Cruise)
            {
                missile.SetThrottle(Mathf.Min(WarewindConstants.CruiseThrottle, 0.35f));
                return;
            }

            if (f.Phase == WarewindPhase.Align)
                missile.SetThrottle(WarewindConstants.PartialThrottle);
            else if (f.Phase == WarewindPhase.Cruise)
                missile.SetThrottle(WarewindConstants.CruiseThrottle);
            else
                missile.SetThrottle(WarewindConstants.FullThrottle);
        }

        private static GlobalPosition BuildAimpoint(
            WarewindFlight f, Vector3 pos, Vector3 tgt, float dist, float alt, Vector3 vel)
        {
            Vector3 toTgt = HorizDir(pos, tgt);
            Vector3 velH = vel;
            velH.y = 0f;
            bool early = f.Phase is WarewindPhase.Drop or WarewindPhase.Align or WarewindPhase.Loft;
            f.OverTopActive = !f.DirectAttack && early && WarewindLevel.TargetBehind(vel, toTgt);
            Vector3 heading = toTgt;

            switch (f.Phase)
            {
                case WarewindPhase.Drop:
                {
                    Vector3 d = f.OverTopActive
                        ? WarewindLevel.OverTopDir(vel, toTgt, WarewindLevel.OverTopPitchDeg(f, vel, toTgt))
                        : WarewindLevel.FlightDir(heading, WarewindConstants.DropPitchDeg);
                    d = WarewindAssist.AimDir(d, vel, f);
                    f.DesiredDir = d;
                    return (pos + d * WarewindConstants.AimLookaheadM).ToGlobalPosition();
                }
                case WarewindPhase.Align:
                case WarewindPhase.Loft:
                case WarewindPhase.Cruise:
                {
                    if (f.Phase == WarewindPhase.Cruise && !f.DirectAttack)
                    {
                        if (!f.CruiseHeadingSet)
                        {
                            f.CruiseHeading = velH.sqrMagnitude > 1f ? velH.normalized : toTgt;
                            f.CruiseHeadingSet = true;
                        }
                        heading = WarewindLevel.SlewHeading(
                            f.CruiseHeading, toTgt, WarewindConstants.CruiseYawSlewDegS);
                        f.CruiseHeading = heading;
                    }

                    float climb = vel.y;
                    float pitch = WarewindLevel.AimPitchDeg(
                        f, f.Phase, alt, f.CruiseAltM, climb, vel.magnitude, vel, toTgt);
                    Vector3 d = f.OverTopActive
                        ? WarewindLevel.OverTopDir(vel, toTgt, pitch)
                        : WarewindLevel.FlightDir(heading, pitch);
                    d = WarewindAssist.AimDir(d, vel, f);
                    f.DesiredDir = d;
                    return (pos + d * WarewindConstants.AimLookaheadM).ToGlobalPosition();
                }
                default:
                    return DiveAim(f, pos, tgt, dist, alt, vel, heading);
            }
        }

        private static GlobalPosition DiveAim(
            WarewindFlight f, Vector3 pos, Vector3 tgt, float dist, float alt, Vector3 vel, Vector3 heading)
        {
            Vector3 toTgt = HorizDir(pos, tgt);
            Vector3 dir;
            if (dist <= WarewindConstants.TerminalDirectDistM && Vector3.Dot(tgt - pos, vel) > 0f)
            {
                Vector3 lead = tgt + f.LastKnownVel * 0.35f;
                dir = lead - pos;
                if (dir.sqrMagnitude < 1f)
                    dir = WarewindLevel.FlightDir(toTgt, -f.DiveAngleMinEff);
            }
            else
            {
                float geom = Mathf.Atan2(Mathf.Max(0f, alt), Mathf.Max(100f, dist)) * Mathf.Rad2Deg;
                float diveDeg = Mathf.Clamp(geom, f.DiveAngleMinEff, f.DiveAngleMaxEff);
                dir = WarewindLevel.FlightDir(toTgt, -diveDeg);
            }

            dir = WarewindAssist.AimDir(dir, vel, f);
            f.DesiredDir = dir.normalized;
            return (pos + dir.normalized * WarewindConstants.AimLookaheadM).ToGlobalPosition();
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
