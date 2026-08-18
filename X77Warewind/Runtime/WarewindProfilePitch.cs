using UnityEngine;

namespace Warewind
{
    /// <summary>Range budget → proportional loft/dive pitch.</summary>
    internal static class WarewindProfilePitch
    {
        internal static void Apply(WarewindFlight f, float rangeM, float cruiseAltM)
        {
            if (f == null)
                return;

            float climbHoriz = HorizForClimb(cruiseAltM, WarewindConstants.LoftPitchMaxDeg);
            float diveHoriz = GlideHoriz(cruiseAltM, WarewindConstants.DiveAngleMinDeg);
            float cruiseSeg = Mathf.Max(
                WarewindConstants.ProfileCruiseSegMinM,
                rangeM * WarewindConstants.ProfileCruiseSegFrac);
            float need = climbHoriz + diveHoriz + cruiseSeg;
            float scale = need > 1f
                ? Mathf.Clamp(rangeM / need, WarewindConstants.PitchScaleMin, 1f)
                : 1f;

            f.PitchScale = scale;
            f.LoftPitchMaxEff = Lerp(WarewindConstants.LoftPitchMinDeg, WarewindConstants.LoftPitchMaxDeg, scale);
            f.LoftPitchShallowEff = Lerp(WarewindConstants.LoftPitchMinDeg, WarewindConstants.LoftPitchShallowDeg, scale);
            f.OverTopPitchEff = Lerp(WarewindConstants.LoftPitchMinDeg, WarewindConstants.OverTopPitchDeg, scale);
            f.DiveAngleMinEff = Lerp(WarewindConstants.DiveAngleFloorDeg, WarewindConstants.DiveAngleMinDeg, scale);
            f.DiveAngleMaxEff = Lerp(WarewindConstants.DiveAngleFloorDeg, WarewindConstants.DiveAngleMaxDeg, scale);
        }

        internal static float GlideHoriz(float altM, float minDiveDeg)
        {
            float a = Mathf.Max(500f, altM);
            float tan = Mathf.Tan(Mathf.Clamp(minDiveDeg, 8f, 89f) * Mathf.Deg2Rad);
            if (tan < 0.2f)
                tan = 0.2f;
            return a / tan + WarewindConstants.DivePullLeadM;
        }

        private static float HorizForClimb(float altM, float pitchDeg)
        {
            float a = Mathf.Max(500f, altM);
            float tan = Mathf.Tan(Mathf.Clamp(pitchDeg, 8f, 89f) * Mathf.Deg2Rad);
            if (tan < 0.08f)
                tan = 0.08f;
            return a / tan;
        }

        private static float Lerp(float floor, float ideal, float scale) =>
            floor + (ideal - floor) * Mathf.Clamp01(scale);
    }
}
