using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Smooth loft→level. Cruise holds alt with pitch PD. Early over-top when tgt astern.
    /// </summary>
    internal static class WarewindLevel
    {
        internal static bool TargetBehind(Vector3 vel, Vector3 toTgtHoriz)
        {
            Vector3 vH = vel;
            vH.y = 0f;
            if (vH.sqrMagnitude < 1f || toTgtHoriz.sqrMagnitude < 0.01f)
                return false;
            return Vector3.Dot(toTgtHoriz.normalized, vH.normalized) < WarewindConstants.TargetBehindDot;
        }

        /// <summary>Pitch current velocity toward tgt azimuth through the vertical (dot&gt;0 safe for ForwardOnly).</summary>
        internal static Vector3 OverTopDir(Vector3 vel, Vector3 toTgtHoriz, float pitchUpDeg)
        {
            if (vel.sqrMagnitude < 4f)
                return FlightDir(toTgtHoriz, pitchUpDeg);

            Vector3 v = vel.normalized;
            Vector3 h = toTgtHoriz.sqrMagnitude > 0.01f ? toTgtHoriz.normalized : Horiz(-v);
            Vector3 pull = (Vector3.up * 2f + h).normalized;
            Vector3 aim = Vector3.RotateTowards(v, pull, pitchUpDeg * Mathf.Deg2Rad, 0f);
            if (Vector3.Dot(aim, v) < 0.12f)
                aim = Vector3.RotateTowards(v, Vector3.up, pitchUpDeg * Mathf.Deg2Rad, 0f);
            return aim.sqrMagnitude > 0.01f ? aim.normalized : v;
        }

        internal static float OverTopPitchDeg(Vector3 vel, Vector3 toTgtHoriz)
        {
            Vector3 vH = vel;
            vH.y = 0f;
            if (vH.sqrMagnitude < 1f || toTgtHoriz.sqrMagnitude < 0.01f)
                return WarewindConstants.OverTopPitchDeg;
            float dot = Vector3.Dot(toTgtHoriz.normalized, vH.normalized);
            float u = Mathf.InverseLerp(-1f, 0.25f, dot);
            return Mathf.Lerp(WarewindConstants.OverTopPitchDeg, WarewindConstants.LoftPitchMaxDeg, u);
        }

        internal static float AimPitchDeg(
            WarewindFlight f, WarewindPhase phase, float alt, float cruise, float climb, float speed,
            Vector3 vel, Vector3 toTgtHoriz)
        {
            if (phase == WarewindPhase.Drop)
                return f.OverTopActive
                    ? OverTopPitchDeg(vel, toTgtHoriz)
                    : WarewindConstants.DropPitchDeg;

            if (f.OverTopActive)
            {
                float pitchTarget = OverTopPitchDeg(vel, toTgtHoriz);
                f.PitchCmd = Mathf.MoveTowards(
                    f.PitchCmd, pitchTarget, WarewindConstants.PitchSlewCatchDegS * Time.fixedDeltaTime);
                return f.PitchCmd;
            }

            float maxPitch = MaxPitchDeg(f, phase);
            float target;
            if (phase == WarewindPhase.Cruise)
            {
                float sp = Mathf.Max(40f, speed);
                float sinMax = Mathf.Sin(maxPitch * Mathf.Deg2Rad);
                float vyMax = sp * sinMax;
                float err = cruise - alt;
                float vyDes = err * WarewindConstants.LevelVyGain - climb * WarewindConstants.LevelClimbDamp;
                vyDes = Mathf.Clamp(vyDes, -vyMax, vyMax);
                target = Mathf.Asin(Mathf.Clamp(vyDes / sp, -sinMax, sinMax)) * Mathf.Rad2Deg;
            }
            else
            {
                float u = Mathf.Clamp01(alt / Mathf.Max(500f, cruise));
                if (u < 0.65f)
                    target = maxPitch;
                else
                    target = Mathf.Lerp(maxPitch, 0f, (u - 0.65f) / 0.35f);
            }

            float slew = phase == WarewindPhase.Cruise
                ? WarewindConstants.PitchSlewCruiseDegS
                : WarewindConstants.PitchSlewLoftDegS;
            if (Mathf.Abs(f.PitchCmd - target) > 6f)
                slew = WarewindConstants.PitchSlewCatchDegS;
            f.PitchCmd = Mathf.MoveTowards(f.PitchCmd, target, slew * Time.fixedDeltaTime);
            return f.PitchCmd;
        }

        internal static Vector3 FlightDir(Vector3 horizDir, float pitchDeg)
        {
            horizDir.y = 0f;
            if (horizDir.sqrMagnitude < 0.01f)
                horizDir = Vector3.forward;
            horizDir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, horizDir);
            if (right.sqrMagnitude < 0.01f)
                right = Vector3.right;
            right.Normalize();
            return (Quaternion.AngleAxis(-pitchDeg, right) * horizDir).normalized;
        }

        internal static Vector3 ForwardOnly(Vector3 dir, Vector3 vel, float maxOffDeg)
        {
            if (vel.sqrMagnitude < 4f)
                return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
            Vector3 v = vel.normalized;
            if (dir.sqrMagnitude < 0.01f)
                return v;
            dir.Normalize();
            if (Vector3.Dot(dir, v) < 0f)
                dir = Vector3.ProjectOnPlane(dir, v);
            if (dir.sqrMagnitude < 0.01f)
                return v;
            dir.Normalize();
            if (Vector3.Angle(v, dir) <= maxOffDeg)
                return dir;
            return Vector3.RotateTowards(v, dir, maxOffDeg * Mathf.Deg2Rad, 0f);
        }

        internal static Vector3 SlewHeading(Vector3 current, Vector3 want, float degS)
        {
            current.y = 0f;
            want.y = 0f;
            if (want.sqrMagnitude < 0.01f)
                return current.sqrMagnitude > 0.01f ? current.normalized : Vector3.forward;
            if (current.sqrMagnitude < 0.01f)
                return want.normalized;
            return Vector3.RotateTowards(
                current.normalized, want.normalized, degS * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        }

        private static float MaxPitchDeg(WarewindFlight f, WarewindPhase phase)
        {
            if (phase == WarewindPhase.Cruise)
                return WarewindConstants.CruisePitchMaxDeg;
            if (f != null && f.ShallowLoft)
                return WarewindConstants.LoftPitchShallowDeg;
            return WarewindConstants.LoftPitchMaxDeg;
        }

        private static Vector3 Horiz(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.01f ? v.normalized : Vector3.forward;
        }
    }
}
