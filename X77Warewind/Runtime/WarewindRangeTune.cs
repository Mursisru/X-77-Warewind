using System.Reflection;
using UnityEngine;

namespace Warewind.Runtime
{
    /// <summary>
    /// Vanilla CalcRange uses sqrt(thrust/drag)*burn with AAM2 finArea → ~780km HUD lie.
    /// Tune calc-only finArea on WarewindCalcProxy so envelope matches DesignRangeM (~450km).
    /// Live flight keeps WarewindAero finArea — HUD reads the proxy only.
    /// </summary>
    internal static class WarewindRangeTune
    {
        private static readonly FieldInfo? FinAreaField =
            typeof(Missile).GetField("finArea", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static float CalcFinAreaM2 { get; private set; } = 1.2f;

        internal static void TuneCalcProxy(Missile missile)
        {
            if (missile == null || FinAreaField == null)
                return;

            float lo = 0.08f;
            float hi = 8f;
            float target = WarewindConstants.DesignRangeM;

            for (int i = 0; i < 28; i++)
            {
                float mid = (lo + hi) * 0.5f;
                FinAreaField.SetValue(missile, mid);
                float refR = SampleReference(missile);
                if (refR > target)
                    lo = mid;
                else
                    hi = mid;
            }

            CalcFinAreaM2 = hi;
            FinAreaField.SetValue(missile, CalcFinAreaM2);
            float finalRef = SampleReference(missile);
            float finalMax = SampleMaxRange(missile);
            WarewindPlugin.ModLog?.LogInfo(
                $"WarewindRangeTune finArea={CalcFinAreaM2:F3} ref={UnitConverter.DistanceReading(finalRef)} max={UnitConverter.DistanceReading(finalMax)}");
        }

        internal static float SampleReference(Missile missile)
        {
            float nez;
            return missile.CalcRange(
                WarewindConstants.CalcRefLaunchSpeedMps,
                WarewindConstants.CalcRefLaunchAltM,
                WarewindConstants.CalcRefTargetAltM,
                WarewindConstants.CalcRefTargetDistM,
                0f,
                out nez);
        }

        private static float SampleMaxRange(Missile missile)
        {
            float max = 0f;
            float[] speeds = { 0f, 180f, 250f, 300f };
            float[] alts = { 0f, 3000f, 8000f, 12000f, 15000f };
            for (int s = 0; s < speeds.Length; s++)
            {
                for (int a = 0; a < alts.Length; a++)
                {
                    float nez;
                    float r = missile.CalcRange(
                        speeds[s], alts[a], 0f, WarewindConstants.CalcRefTargetDistM, 0f, out nez);
                    if (r > max)
                        max = r;
                }
            }

            return max;
        }
    }
}
