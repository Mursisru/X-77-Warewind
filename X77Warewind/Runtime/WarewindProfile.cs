using UnityEngine;

namespace Warewind
{
    /// <summary>Dive by live glide geometry (d8886d8). Commit = alt/tan(min)+lead, range×0.55 cap.</summary>
    internal static class WarewindProfile
    {
        internal static void Lock(WarewindFlight f, Vector3 launchPos, Vector3 targetPos)
        {
            if (f == null)
                return;

            float range = Horiz(launchPos, targetPos);
            float launchAlt = Mathf.Max(0f, launchPos.y - Datum.LocalSeaY);
            f.LockRangeM = range;
            float cruiseAlt = CruiseForRange(range);
            f.CruiseAltM = cruiseAlt;
            WarewindProfilePitch.Apply(f, range, cruiseAlt);
            f.DirectAttack = range < WarewindConstants.DirectAttackRangeM;
            if (f.DirectAttack)
            {
                f.CruiseAltM = Mathf.Min(
                    f.CruiseAltM,
                    launchAlt + 600f,
                    range * 0.28f);
                f.CruiseAltM = Mathf.Max(f.CruiseAltM, 400f);
                f.ShallowLoft = true;
            }
            else
                f.ShallowLoft = f.PitchScale < 0.55f || range < WarewindConstants.ShallowLoftRangeM;

            f.LoftEnterAltM = Mathf.Max(f.CruiseAltM * 0.96f, f.CruiseAltM - 400f);
            f.LevelStartAltM = Mathf.Max(500f, f.CruiseAltM * 0.55f);
            f.DiveCommitDistM = DiveCommitForRange(range, f.CruiseAltM, f.DiveAngleMinEff);
            f.ProfileLocked = true;

            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind profile range={range * 0.001f:F1}km cruise={f.CruiseAltM * 0.001f:F1}km " +
                $"dive@{f.DiveCommitDistM * 0.001f:F1}km pitchScale={f.PitchScale:F2} direct={f.DirectAttack} " +
                $"loft={f.LoftPitchMaxEff:F0}° dive={f.DiveAngleMinEff:F0}-{f.DiveAngleMaxEff:F0}°");
        }

        internal static void Ensure(WarewindFlight f, Vector3 pos, Vector3 tgt)
        {
            if (f == null || f.ProfileLocked)
                return;
            Lock(f, pos, tgt);
        }

        /// <summary>Horiz dist to start dive: alt/tan(min dive) + pull lead.</summary>
        internal static float GlideDistM(float altM, float minDiveDeg) =>
            WarewindProfilePitch.GlideHoriz(altM, minDiveDeg);

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

        internal static float DiveCommitForRange(float rangeM, float cruiseAltM, float minDiveDeg)
        {
            float glide = GlideDistM(cruiseAltM, minDiveDeg);
            if (rangeM < WarewindConstants.DirectAttackRangeM)
                return Mathf.Clamp(Mathf.Min(glide, rangeM * 0.88f), 1500f, rangeM * 0.95f);
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
