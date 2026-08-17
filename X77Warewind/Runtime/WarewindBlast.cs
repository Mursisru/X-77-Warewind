using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// AAM2 warhead FX are ~35kg-class: yield≤200 never calls Shockwave.SetOwner (uses baked VFX).
    /// yield&gt;200 needs Shockwave on airEffect — AAM2 often has none → zero HE. Stamp TBM effects + 700kg.
    /// </summary>
    internal static class WarewindBlast
    {
        private static readonly FieldInfo? BlastYieldField =
            typeof(Missile).GetField("blastYield", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? WarheadField =
            typeof(Missile).GetField("warhead", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? AirEffectField =
            typeof(Missile.Warhead).GetField("airEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ArmorEffectField =
            typeof(Missile.Warhead).GetField("armorEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? TerrainEffectField =
            typeof(Missile.Warhead).GetField("terrainEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? WaterSurfaceEffectField =
            typeof(Missile.Warhead).GetField("waterSurfaceEffect", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? UnderwaterEffectField =
            typeof(Missile.Warhead).GetField("underwaterEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static GameObject? _air;
        private static GameObject? _armor;
        private static GameObject? _terrain;
        private static GameObject? _water;
        private static GameObject? _under;
        private static bool _captured;
        private static bool _airHasShockwave;

        internal static bool NeedsFragFallback => _captured && !_airHasShockwave;

        internal static void CaptureTbm(Encyclopedia enc)
        {
            if (_captured || enc?.missiles == null)
                return;

            Missile? donor = null;
            int best = -1;
            for (int i = 0; i < enc.missiles.Count; i++)
            {
                MissileDefinition? def = enc.missiles[i];
                if (def?.unitPrefab == null || string.IsNullOrEmpty(def.jsonKey))
                    continue;
                string k = def.jsonKey;
                int s = 0;
                if (k.Equals("BallisticMissile1", System.StringComparison.OrdinalIgnoreCase))
                    s = 100;
                else if (k.StartsWith("BallisticMissile1", System.StringComparison.OrdinalIgnoreCase))
                    s = 80;
                else if (k.IndexOf("BallisticMissile", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    s = 40;
                if (s <= best)
                    continue;
                Missile? m = def.unitPrefab.GetComponent<Missile>()
                             ?? def.unitPrefab.GetComponentInChildren<Missile>(true);
                if (m == null)
                    continue;
                best = s;
                donor = m;
            }

            if (donor == null || WarheadField == null)
            {
                WarewindPlugin.ModLog?.LogWarning("Warewind blast: no TBM warhead donor.");
                return;
            }

            if (WarheadField.GetValue(donor) is not Missile.Warhead wh)
                return;

            _air = AirEffectField?.GetValue(wh) as GameObject;
            _armor = ArmorEffectField?.GetValue(wh) as GameObject;
            _terrain = TerrainEffectField?.GetValue(wh) as GameObject;
            _water = WaterSurfaceEffectField?.GetValue(wh) as GameObject;
            _under = UnderwaterEffectField?.GetValue(wh) as GameObject;
            _airHasShockwave = _air != null && _air.GetComponentInChildren<Shockwave>(true) != null;
            _captured = _air != null || _armor != null || _terrain != null;

            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind blast TBM FX air={(_air != null)} shockwave={_airHasShockwave} armor={(_armor != null)} fallbackFrag={NeedsFragFallback}");
        }

        internal static void Ensure(Missile missile)
        {
            if (missile == null)
                return;

            BlastYieldField?.SetValue(missile, WarewindConstants.BlastYieldKg);
            StampWarheadFx(missile);
        }

        private static void StampWarheadFx(Missile missile)
        {
            if (!_captured || WarheadField == null)
                return;
            if (WarheadField.GetValue(missile) is not Missile.Warhead wh)
                return;

            // Prefer TBM airEffect that carries Shockwave (required for yield>200 damage path).
            if (_air != null)
                AirEffectField?.SetValue(wh, _air);
            if (_armor != null)
                ArmorEffectField?.SetValue(wh, _armor);
            if (_terrain != null)
                TerrainEffectField?.SetValue(wh, _terrain);
            if (_water != null)
                WaterSurfaceEffectField?.SetValue(wh, _water);
            if (_under != null)
                UnderwaterEffectField?.SetValue(wh, _under);
        }

        /// <summary>
        /// If TBM FX still lack Shockwave, deal HE via BlastFrag (same scale as yield kg TNT).
        /// </summary>
        internal static void FallbackBlast(Missile missile, Vector3 position)
        {
            if (missile == null)
                return;
            Ensure(missile);
            DamageEffects.BlastFrag(WarewindConstants.BlastYieldKg, position, missile.ownerID, PersistentID.None);
        }
    }
}
