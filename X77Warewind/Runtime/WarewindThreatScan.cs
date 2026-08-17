using System.Reflection;
using UnityEngine;

namespace Warewind
{
    internal enum WarewindThreatKind
    {
        None,
        Radar,
        Ir
    }

    /// <summary>Hostile inbound seeker — lock optional, closing geometry enough.</summary>
    internal static class WarewindThreatScan
    {
        private static readonly FieldInfo? SeekerTarget =
            typeof(MissileSeeker).GetField("targetUnit", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TryFind(Missile self, out Missile threat, out WarewindThreatKind kind)
        {
            threat = null!;
            kind = WarewindThreatKind.None;
            if (self == null)
                return false;

            System.Collections.Generic.List<Unit> units = UnitRegistry.allUnits;
            if (units == null)
                return false;

            PersistentID id = self.persistentID;
            Missile? best = null;
            WarewindThreatKind bestKind = WarewindThreatKind.None;
            float bestD = float.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                if (!(units[i] is Missile m) || m == null || m.disabled || m == self)
                    continue;
                if (!IsHostile(self, m))
                    continue;
                if (!TryClassify(self, m, id, out WarewindThreatKind k))
                    continue;

                float d = (m.transform.position - self.transform.position).sqrMagnitude;
                if (d >= bestD)
                    continue;
                bestD = d;
                best = m;
                bestKind = k;
            }

            if (best == null)
                return false;
            threat = best;
            kind = bestKind;
            return true;
        }

        private static bool TryClassify(Missile self, Missile inbound, PersistentID selfId, out WarewindThreatKind kind)
        {
            kind = WarewindThreatKind.None;
            if (!IsClosing(self, inbound))
                return false;

            bool locked = (selfId.IsValid && inbound.targetID.IsValid && inbound.targetID == selfId)
                            || GetSeekerTarget(inbound) == self;

            if (inbound.GetComponent<ARHSeeker>() != null || inbound.GetComponent<SARHSeeker>() != null)
            {
                kind = WarewindThreatKind.Radar;
                return locked || NoseToward(self, inbound);
            }

            if (inbound.GetComponent<IRSeeker>() != null)
            {
                kind = WarewindThreatKind.Ir;
                return locked || NoseToward(self, inbound);
            }

            return false;
        }

        private static bool IsHostile(Missile self, Missile inbound)
        {
            if (self.NetworkHQ != null && inbound.NetworkHQ != null)
                return inbound.NetworkHQ != self.NetworkHQ;
            return true;
        }

        private static bool IsClosing(Missile self, Missile inbound)
        {
            Vector3 toSelf = self.transform.position - inbound.transform.position;
            float dist = toSelf.magnitude;
            if (dist > WarewindConstants.ThreatDetectRangeM || dist < 30f)
                return false;

            Vector3 vel = inbound.rb != null && inbound.rb.velocity.sqrMagnitude > 100f
                ? inbound.rb.velocity
                : inbound.transform.forward * Mathf.Max(inbound.speed, 80f);
            if (vel.sqrMagnitude < 100f)
                return false;

            Vector3 toN = toSelf / dist;
            return Vector3.Dot(vel.normalized, toN) >= WarewindConstants.ThreatClosingDotMin;
        }

        private static bool NoseToward(Missile self, Missile inbound)
        {
            Vector3 toSelf = self.transform.position - inbound.transform.position;
            if (toSelf.sqrMagnitude < 1f)
                return true;
            return Vector3.Angle(inbound.transform.forward, toSelf) <= WarewindConstants.ThreatAimConeDeg;
        }

        private static Unit? GetSeekerTarget(Missile inbound)
        {
            MissileSeeker? s = inbound.GetComponent<MissileSeeker>();
            if (s == null || SeekerTarget == null)
                return null;
            return SeekerTarget.GetValue(s) as Unit;
        }
    }
}
