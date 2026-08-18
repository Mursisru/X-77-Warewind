using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Thrust is along the body. Bend velocity toward nose when nose≠vel — works with or without fuel.
    /// </summary>
    internal static class WarewindAssist
    {
        private static readonly FieldInfo? ThrottleField =
            typeof(Missile).GetField("throttle", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static Vector3 AimDir(Vector3 want, Vector3 vel, WarewindFlight f)
        {
            if (f == null)
                return WarewindLevel.ForwardOnly(want, vel, WarewindConstants.AimMaxOffMidDeg);

            if (f.DirectAttack)
            {
                if (want.sqrMagnitude < 0.01f)
                    return vel.sqrMagnitude > 0.01f ? vel.normalized : Vector3.forward;
                want.Normalize();
                if (vel.sqrMagnitude < 4f)
                    return want;
                return Vector3.RotateTowards(
                    vel.normalized, want, WarewindConstants.AimMaxOffDirectDeg * Mathf.Deg2Rad, 0f);
            }

            float cap;
            if (f.OverTopActive)
                cap = WarewindConstants.OverTopAimMaxOffDeg;
            else if (f.Phase == WarewindPhase.Dive)
                cap = WarewindConstants.AimMaxOffDiveDeg;
            else if (f.Phase == WarewindPhase.Cruise)
                cap = WarewindConstants.AimMaxOffCruiseDeg;
            else
                cap = WarewindConstants.AimMaxOffMidDeg;
            return WarewindLevel.ForwardOnly(want, vel, cap);
        }

        internal static void FollowNose(Missile missile, WarewindFlight f, bool fueled)
        {
            if (missile?.rb == null || f == null || f.Phase == WarewindPhase.Drop)
                return;

            float throttle = 1f;
            if (fueled)
            {
                throttle = ThrottleField?.GetValue(missile) is float th ? th : 0f;
                if (throttle < 0.05f && f.Phase != WarewindPhase.Dive)
                    return;
            }

            Vector3 v = missile.rb.velocity;
            float sp = v.magnitude;
            if (sp < 20f)
                return;

            Vector3 nose = missile.transform.forward;
            Vector3 target;
            if (f.OverTopActive)
                target = nose.normalized * sp;
            else
            {
                Vector3 vH = new Vector3(v.x, 0f, v.z);
                if (vH.sqrMagnitude < 1f)
                    return;

                Vector3 noseH = new Vector3(nose.x, 0f, nose.z);
                if (!f.DirectAttack && Vector3.Dot(noseH, vH) < 0.08f && fueled)
                    return;

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

            float baseDeg = fueled
                ? WarewindConstants.CrossThrustMaxDegS
                : WarewindConstants.GlideCrossThrustMaxDegS;
            float degS = baseDeg * u;
            if (fueled)
                degS *= throttle;
            if (f.DirectAttack)
                degS = Mathf.Max(degS, baseDeg * 0.85f);
            else if (f.Phase == WarewindPhase.Loft)
                degS *= WarewindConstants.LoftCrossThrustScale;
            else if (f.Phase == WarewindPhase.Cruise && fueled)
                degS = baseDeg * Mathf.Max(u, 0.55f) * throttle;
            else if (f.Phase == WarewindPhase.Dive)
                degS *= WarewindConstants.DiveCrossThrustScale;

            float rad = degS * Mathf.Deg2Rad * Time.fixedDeltaTime;
            if (rad < 1e-6f)
                return;

            missile.rb.velocity = Vector3.RotateTowards(v, target, rad, 0f);
        }
    }
}
