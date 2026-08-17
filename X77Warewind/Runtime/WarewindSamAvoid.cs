using UnityEngine;

namespace Warewind
{
    /// <summary>Soft lateral aim offset around IR_SAM / R_SAM / AAA cylinders until 15 km.</summary>
    internal static class WarewindSamAvoid
    {
        internal static Vector3 OffsetCruise(Vector3 pos, Vector3 aim, Missile missile)
        {
            if (missile == null)
                return aim;

            Vector3 toAim = aim - pos;
            toAim.y = 0f;
            float remain = Horiz(pos, aim);
            if (remain <= WarewindConstants.SamAvoidUntilM)
                return aim;

            Vector3 offset = Vector3.zero;
            int n = 0;
            System.Collections.Generic.List<Unit> units = UnitRegistry.allUnits;
            if (units == null)
                return aim;

            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null || u.disabled)
                    continue;
                if (missile.NetworkHQ != null && u.NetworkHQ == missile.NetworkHQ)
                    continue;
                if (!IsSam(u))
                    continue;

                float range = u.GetMaxRange();
                if (range < 500f)
                    continue;

                Vector3 p = u.transform.position;
                float dist = DistPointToSeg(p, pos, aim);
                if (dist >= range)
                    continue;

                Vector3 away = pos - p;
                away.y = 0f;
                if (away.sqrMagnitude < 1f)
                    away = Vector3.Cross(Vector3.up, toAim);
                away.Normalize();
                float penetrate = range - dist;
                offset += away * Mathf.Min(penetrate, WarewindConstants.SamMaxDetourM);
                n++;
            }

            if (n == 0)
                return aim;
            offset /= n;
            if (offset.sqrMagnitude > WarewindConstants.SamMaxDetourM * WarewindConstants.SamMaxDetourM)
                offset = offset.normalized * WarewindConstants.SamMaxDetourM;
            Vector3 result = aim + offset;
            result.y = aim.y;
            return result;
        }

        private static bool IsSam(Unit u)
        {
            if (u.definition is VehicleDefinition vd)
            {
                VehicleType t = vd.vehicleType;
                return t == VehicleType.IR_SAM || t == VehicleType.R_SAM || t == VehicleType.AAA;
            }
            return u is Ship && u.GetMaxRange() > 4000f;
        }

        private static float Horiz(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float DistPointToSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            ab.y = 0f;
            Vector3 ap = p - a;
            ap.y = 0f;
            float ab2 = ab.sqrMagnitude;
            if (ab2 < 1f)
                return Horiz(p, a);
            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / ab2);
            Vector3 proj = a + ab * t;
            return Horiz(p, proj);
        }
    }
}
