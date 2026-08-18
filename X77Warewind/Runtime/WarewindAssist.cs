using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Thrust is along the body. FX stays on the nozzle.
    /// At Mach 6 a_perp/v is ~0.3°/s so vel ignores the nose — rotate vel onto forward while motor burns.
    /// </summary>
    internal static class WarewindAssist
    {
        private static readonly FieldInfo? ThrottleField =
            typeof(Missile).GetField("throttle", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static Vector3 AimDir(Vector3 want, Vector3 vel, WarewindPhase phase, bool overTop = false)
        {
            float cap = overTop
                ? WarewindConstants.OverTopAimMaxOffDeg
                : phase == WarewindPhase.Dive
                    ? WarewindConstants.AimMaxOffDiveDeg
                    : phase == WarewindPhase.Cruise
                        ? WarewindConstants.AimMaxOffCruiseDeg
                        : WarewindConstants.AimMaxOffMidDeg;
            return WarewindLevel.ForwardOnly(want, vel, cap);
        }

        internal static void FollowNose(Missile missile, WarewindFlight f, bool fuel)
        {
            if (missile?.rb == null || f == null || !fuel || f.Phase == WarewindPhase.Drop)
                return;

            float throttle = ThrottleField?.GetValue(missile) is float th ? th : 0f;
            if (throttle < 0.05f)
                return;

            Vector3 v = missile.rb.velocity;
            float sp = v.magnitude;
            if (sp < 20f)
                return;

            Vector3 nose = missile.transform.forward;
            Vector3 vH = new Vector3(v.x, 0f, v.z);
            if (vH.sqrMagnitude < 1f)
                return;

            Vector3 noseH = new Vector3(nose.x, 0f, nose.z);
            if (!f.OverTopActive && Vector3.Dot(noseH, vH) < 0.08f)
                return;

            Vector3 target;
            if (f.OverTopActive)
                target = nose.normalized * sp;
            else
            {
                float desVy = sp * nose.y;
                float vyCap = sp * 0.95f;
                desVy = Mathf.Clamp(desVy, -vyCap, vyCap);
                float h = Mathf.Sqrt(Mathf.Max(1f, sp * sp - desVy * desVy));
                target = vH.normalized * h + Vector3.up * desVy;
            }

            float off = Vector3.Angle(v, target);
            if (off < WarewindConstants.CrossThrustMinOffDeg)
                return;

            float u = Mathf.InverseLerp(
                WarewindConstants.CrossThrustMinOffDeg,
                WarewindConstants.CrossThrustFullOffDeg,
                off);
            float degS = WarewindConstants.CrossThrustMaxDegS * u * throttle;
            if (f.Phase == WarewindPhase.Cruise)
                degS = WarewindConstants.CrossThrustMaxDegS * Mathf.Max(u, 0.55f);
            float rad = degS * Mathf.Deg2Rad * Time.fixedDeltaTime;
            if (rad < 1e-6f)
                return;

            missile.rb.velocity = Vector3.RotateTowards(v, target, rad, 0f);
        }
    }
}
