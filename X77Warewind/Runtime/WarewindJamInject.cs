using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>RpcJam often misses seeker state — inject jamAccumulation on server each tick.</summary>
    internal static class WarewindJamInject
    {
        private static readonly FieldInfo? ArhAccum =
            typeof(ARHSeeker).GetField("jamAccumulation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ArhTol =
            typeof(ARHSeeker).GetField("jamTolerance", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ArhJammed =
            typeof(ARHSeeker).GetField("isJammed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? SarhAccum =
            typeof(SARHSeeker).GetField("jamAccumulation", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? SarhTol =
            typeof(SARHSeeker).GetField("jamTolerance", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? SarhJammed =
            typeof(SARHSeeker).GetField("isJammed", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Pulse(Missile inbound, Missile self, float amount)
        {
            if (inbound == null || self == null || amount <= 0f)
                return;

            Unit.JamEventArgs args = new Unit.JamEventArgs
            {
                jamAmount = amount,
                jammingUnit = self
            };
            inbound.Jam(args);
            ApplyDirect(inbound, amount);
        }

        internal static void ApplyArh(ARHSeeker seeker, Unit.JamEventArgs e, float amount)
        {
            if (seeker == null || amount <= 0f)
                return;
            float acc = Read(ArhAccum, seeker) + amount;
            float tol = Read(ArhTol, seeker);
            if (tol <= 0.01f)
                tol = 0.35f;
            acc = Mathf.Clamp01(acc);
            ArhAccum?.SetValue(seeker, acc);
            if (acc > tol)
                ArhJammed?.SetValue(seeker, true);
            if (e.jammingUnit != null)
            {
                Missile? m = seeker.GetComponent<Missile>();
                m?.RecordDamage(e.jammingUnit.persistentID, 0.01f);
            }
        }

        internal static void ApplyDirect(Missile inbound, float amount)
        {
            ARHSeeker? arh = inbound.GetComponent<ARHSeeker>();
            if (arh != null)
            {
                ApplyArh(arh, new Unit.JamEventArgs { jamAmount = amount }, amount);
                return;
            }

            SARHSeeker? sarh = inbound.GetComponent<SARHSeeker>();
            if (sarh == null)
                return;
            float acc = Read(SarhAccum, sarh);
            float tol = Read(SarhTol, sarh);
            if (tol <= 0.01f)
                tol = 0.35f;
            acc += amount / tol;
            acc = Mathf.Clamp01(acc);
            SarhAccum?.SetValue(sarh, acc);
            if (acc > tol)
                SarhJammed?.SetValue(sarh, true);
        }

        private static float Read(FieldInfo? f, object o)
        {
            if (f == null)
                return 0f;
            object? v = f.GetValue(o);
            return v is float n ? n : 0f;
        }
    }
}
