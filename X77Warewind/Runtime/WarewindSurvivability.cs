using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>Hypersonic body — stop API/prox one-shots; bullets use ArmorPenetrate vs pierceArmor.</summary>
    internal static class WarewindSurvivability
    {
        private static readonly FieldInfo? Hitpoints =
            typeof(Missile).GetField("hitpoints", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? Armor =
            typeof(Missile).GetField("armorProperties", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void ApplyDefinition(MissileDefinition? def)
        {
            if (def == null)
                return;
            def.armorTier = WarewindConstants.BodyArmorTier;
        }

        internal static void Apply(Missile missile)
        {
            if (missile == null)
                return;

            if (missile.definition is MissileDefinition md)
                md.armorTier = WarewindConstants.BodyArmorTier;

            SetHp(missile, WarewindConstants.BodyHitpoints);

            if (Armor?.GetValue(missile) is ArmorProperties ap)
            {
                ap.pierceArmor = WarewindConstants.BodyPierceArmor;
                ap.blastArmor = WarewindConstants.BodyBlastArmor;
                ap.fireArmor = WarewindConstants.BodyFireArmor;
                ap.pierceTolerance = WarewindConstants.BodyPierceTolerance;
                ap.blastTolerance = WarewindConstants.BodyBlastTolerance;
                ap.fireTolerance = WarewindConstants.BodyFireTolerance;
            }
        }

        /// <summary>Vanilla TakeDamage impact branch = instant Detonate — never use it.</summary>
        internal static bool ProcessDamage(
            Missile missile,
            float pierceDamage,
            float blastDamage,
            float amountAffected,
            float fireDamage,
            PersistentID dealerId)
        {
            if (missile == null || missile.disabled)
                return true;

            Apply(missile);

            if (dealerId == missile.persistentID || dealerId == missile.ownerID || dealerId.NotValid)
                return true;

            ArmorProperties ap = missile.GetArmorProperties();
            if (pierceDamage <= ap.pierceArmor && blastDamage <= ap.blastArmor && fireDamage <= ap.fireArmor)
                return true;

            float p = Mathf.Max(pierceDamage - ap.pierceArmor, 0f) / Mathf.Max(ap.pierceTolerance, 0.1f);
            float b = Mathf.Max(blastDamage - ap.blastArmor, 0f) * amountAffected / Mathf.Max(ap.blastTolerance, 0.1f);
            float f = Mathf.Max(fireDamage - ap.fireArmor, 0f) / Mathf.Max(ap.fireTolerance, 0.1f);
            float loss = (p + b + f) * WarewindConstants.IncomingDamageScale;
            if (loss <= 0.001f)
                return true;

            float hp = GetHp(missile) - loss;
            SetHp(missile, hp);
            if (hp > 0f)
                return true;

            PersistentUnit dealer;
            if (UnitRegistry.TryGetPersistentUnit(dealerId, out dealer) && dealer.GetHQ() != missile.NetworkHQ)
            {
                missile.RecordDamage(dealerId, 1000f);
                missile.ReportKilled();
            }

            WarewindFuse.AllowDetonate = true;
            try
            {
                Vector3 n = missile.rb != null ? missile.rb.velocity : missile.transform.forward;
                missile.Detonate(n, false, false);
            }
            finally
            {
                WarewindFuse.AllowDetonate = false;
            }
            return true;
        }

        internal static float GetHp(Missile missile)
        {
            if (Hitpoints?.GetValue(missile) is float hp)
                return hp;
            return WarewindConstants.BodyHitpoints;
        }

        private static void SetHp(Missile missile, float hp)
        {
            Hitpoints?.SetValue(missile, Mathf.Max(0f, hp));
        }
    }
}
