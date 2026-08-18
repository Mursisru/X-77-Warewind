using UnityEngine;

namespace Warewind
{
    /// <summary>Dive at 45–60° onto the target. Commit = alt/tan(45°) + pull lead (~58km from 50km, not 124).</summary>
    internal static class WarewindProfile
    {
        internal static void Lock(WarewindFlight f, Vector3 launchPos, Vector3 targetPos)
        {
            if (f == null)
                return;

            float range = Horiz(launchPos, targetPos);
            f.LockRangeM = range;
            f.CruiseAltM = CruiseForRange(range);
            f.LoftEnterAltM = Mathf.Max(f.CruiseAltM * 0.96f, f.CruiseAltM - 400f);
            f.LevelStartAltM = Mathf.Max(500f, f.CruiseAltM * 0.55f);
            f.DiveCommitDistM = DiveCommitForRange(range, f.CruiseAltM);
            f.ShallowLoft = range < WarewindConstants.ShallowLoftRangeM;
            f.ProfileLocked = true;

            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind profile range={range * 0.001f:F1}km cruise={f.CruiseAltM * 0.001f:F1}km dive@{f.DiveCommitDistM * 0.001f:F1}km shallow={f.ShallowLoft}");
        }

        internal static void Ensure(WarewindFlight f, Vector3 pos, Vector3 tgt)
        {
            if (f == null || f.ProfileLocked)
                return;
            Lock(f, pos, tgt);
        }

        /// <summary>Horiz dist to start dive: 45° line from cruise alt + lead to pull the nose down.</summary>
        internal static float GlideDistM(float altM)
        {
            float a = Mathf.Max(500f, altM);
            float tan = Mathf.Tan(WarewindConstants.DiveAngleMinDeg * Mathf.Deg2Rad);
            if (tan < 0.2f)
                tan = 0.2f;
            return a / tan + WarewindConstants.DivePullLeadM;
        }

        internal static float CruiseForRange(float rangeM)
        {
            float r = Mathf.Max(0f, rangeM);
            float alt;
            if (r < 25000f)
                alt = Mathf.Lerp(1500f, 6000f, r / 25000f);
            else if (r < 50000f)
                alt = Mathf.Lerp(6000f, 14000f, (r - 25000f) / 25000f);
            else if (r < 100000f)
                alt = Mathf.Lerp(14000f, 30000f, (r - 50000f) / 50000f);
            else if (r < 160000f)
                alt = Mathf.Lerp(30000f, 50000f, (r - 100000f) / 60000f);
            else
                alt = WarewindConstants.CruiseAltMaxM;

            return Mathf.Clamp(alt, WarewindConstants.CruiseAltMinM, WarewindConstants.CruiseAltMaxM);
        }

        internal static float DiveCommitForRange(float rangeM, float cruiseAltM)
        {
            float glide = GlideDistM(cruiseAltM);
            float commit = Mathf.Min(glide, rangeM * 0.55f);
            return Mathf.Clamp(commit, WarewindConstants.DiveCommitDistMinM, WarewindConstants.DiveCommitDistMaxM);
        }

        private static float Horiz(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
