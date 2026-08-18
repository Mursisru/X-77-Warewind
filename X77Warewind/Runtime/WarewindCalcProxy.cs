using UnityEngine;
using Warewind.Bootstrap;
using Warewind.Runtime;

namespace Warewind
{
    /// <summary>Inactive AAM2 clone with Warewind motors — vanilla Missile.CalcRange for HUD.</summary>
    internal static class WarewindCalcProxy
    {
        private static Missile? _missile;

        internal static void Init(Encyclopedia enc)
        {
            if (_missile != null || enc == null)
                return;

            MissileDefinition? aam = PrefabFactory.FindMissileByExactKey(enc, WarewindConstants.ShellMissileKey);
            if (aam?.unitPrefab == null)
                aam = PrefabFactory.FindMissileByExactKey(enc, WarewindConstants.ShellMissileKeyAlt);
            if (aam?.unitPrefab == null)
            {
                WarewindPlugin.ModLog?.LogWarning("WarewindCalcProxy: no AAM2 donor.");
                return;
            }

            GameObject go = Object.Instantiate(aam.unitPrefab);
            go.name = "WarewindCalcProxy";
            go.SetActive(false);
            Object.DontDestroyOnLoad(go);
            Runtime.NetworkPrefabPrep.PrepareTemplate(go);

            _missile = go.GetComponent<Missile>() ?? go.GetComponentInChildren<Missile>(true);
            if (_missile == null)
            {
                Object.Destroy(go);
                WarewindPlugin.ModLog?.LogWarning("WarewindCalcProxy: no Missile component.");
                return;
            }

            WarewindMotors.Apply(_missile);
            WarewindRangeTune.TuneCalcProxy(_missile);
            CacheEncyclopediaStats();
            WarewindPlugin.ModLog?.LogInfo(
                $"WarewindCalcProxy ready range={UnitConverter.DistanceReading(EncyclopediaRangeM)} burn={EncyclopediaBurnS:F0}s dV={UnitConverter.SpeedReading(EncyclopediaDeltaVMps)}.");
        }

        internal static float EncyclopediaRangeM { get; private set; }
        internal static float EncyclopediaDeltaVMps { get; private set; }
        internal static float EncyclopediaBurnS { get; private set; }

        private static void CacheEncyclopediaStats()
        {
            if (_missile == null)
                return;
            EncyclopediaBurnS = _missile.GetTotalBurnTime();
            EncyclopediaDeltaVMps = _missile.CalcDeltaV();
            float nez;
            EncyclopediaRangeM = _missile.CalcRange(
                WarewindConstants.CalcRefLaunchSpeedMps,
                WarewindConstants.CalcRefLaunchAltM,
                WarewindConstants.CalcRefTargetAltM,
                WarewindConstants.CalcRefTargetDistM,
                0f,
                out nez);
            if (EncyclopediaRangeM < 1000f)
                EncyclopediaRangeM = WarewindConstants.DesignRangeM;
            EncyclopediaRangeM = Mathf.Min(EncyclopediaRangeM, WarewindConstants.DesignRangeM);
        }

        internal static float CalcRange(
            float launchSpeed,
            float launchAltitude,
            float targetAltitude,
            float targetDist,
            float targetRelativeSpeed,
            out float noEscapeDistance)
        {
            if (_missile != null)
            {
                float range = _missile.CalcRange(
                    launchSpeed, launchAltitude, targetAltitude, targetDist, targetRelativeSpeed, out noEscapeDistance);
                return Mathf.Min(range, WarewindConstants.DesignRangeM);
            }

            noEscapeDistance = WarewindConstants.DesignRangeM * 0.65f;
            return WarewindConstants.DesignRangeM;
        }
    }
}
