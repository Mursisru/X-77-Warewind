using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Warewind.Runtime
{
    /// <summary>Encyclopedia reads AAM2 motors on unitPrefab — override with WarewindCalcProxy stats.</summary>
    internal static class WarewindEncyclopediaStats
    {
        private static readonly FieldInfo? RangeTextField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "range");
        private static readonly FieldInfo? BurnTextField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "burnTime");
        private static readonly FieldInfo? DeltaVTextField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "deltaV");
        private static readonly FieldInfo? TopSpeedTextField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "topSpeed");
        private static readonly FieldInfo? RangePanelField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "rangePanel");
        private static readonly FieldInfo? BurnPanelField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "burnTimePanel");
        private static readonly FieldInfo? DeltaVPanelField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "deltaVPanel");
        private static readonly FieldInfo? TopSpeedPanelField =
            AccessTools.Field(typeof(EncyclopediaBrowser), "topSpeedPanel");

        internal static void ApplyMissilePanels(EncyclopediaBrowser browser)
        {
            if (browser == null)
                return;

            float rangeM = WarewindCalcProxy.EncyclopediaRangeM;
            float burnS = WarewindCalcProxy.EncyclopediaBurnS;
            float deltaVMps = WarewindCalcProxy.EncyclopediaDeltaVMps;
            if (rangeM < 1000f)
                rangeM = WarewindConstants.DesignRangeM;
            if (burnS < 1f)
                burnS = WarewindConstants.TotalBurnS;
            if (deltaVMps < 1f)
                deltaVMps = WarewindConstants.SustainerTopSpeedMps;

            float topSpeedMps = WarewindConstants.SustainerTopSpeedMps;

            SetText(RangeTextField, browser, UnitConverter.DistanceReading(rangeM));
            SetText(BurnTextField, browser, string.Format("{0:F0}s", burnS));
            SetText(DeltaVTextField, browser, UnitConverter.SpeedReading(deltaVMps));
            SetText(TopSpeedTextField, browser, UnitConverter.SpeedReading(topSpeedMps));

            SetPanel(RangePanelField, browser, deltaVMps > 0f);
            SetPanel(BurnPanelField, browser, deltaVMps > 0f);
            if (topSpeedMps >= deltaVMps)
            {
                SetPanel(DeltaVPanelField, browser, false);
                SetPanel(TopSpeedPanelField, browser, true);
            }
            else
            {
                SetPanel(DeltaVPanelField, browser, true);
                SetPanel(TopSpeedPanelField, browser, false);
            }
        }

        internal static void ApplyTargetRequirements(WeaponInfo info)
        {
            if (info == null)
                return;
            TargetRequirements tr = info.targetRequirements;
            tr.maxRange = WarewindCalcProxy.EncyclopediaRangeM > 1000f
                ? WarewindCalcProxy.EncyclopediaRangeM
                : WarewindConstants.DesignRangeM;
            tr.minRange = WarewindConstants.EncyclopediaMinRangeM;
            info.targetRequirements = tr;
        }

        private static void SetText(FieldInfo? field, EncyclopediaBrowser browser, string value)
        {
            object? tmp = field?.GetValue(browser);
            if (tmp == null)
                return;
            PropertyInfo? p = tmp.GetType().GetProperty("text");
            p?.SetValue(tmp, value);
        }

        private static void SetPanel(FieldInfo? field, EncyclopediaBrowser browser, bool active)
        {
            if (field?.GetValue(browser) is GameObject go)
                go.SetActive(active);
        }
    }
}
