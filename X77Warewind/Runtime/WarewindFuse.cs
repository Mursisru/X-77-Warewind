using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Hydra blocks vanilla Arm/Detonate during spawn. AAM2 StartMissile.Arm() when owner
    /// lookup misses, and TakeDamage(impact) from overlapping colliders, explode the same frame.
    /// </summary>
    internal static class WarewindFuse
    {
        internal static bool AllowArm;
        internal static bool AllowDetonate;

        internal static void ArmNow(Missile missile)
        {
            if (missile == null)
                return;
            AllowArm = true;
            try
            {
                missile.Arm();
            }
            finally
            {
                AllowArm = false;
            }
        }

        internal static void DetonateNow(Missile missile)
        {
            if (missile == null)
                return;
            WarewindBlast.Ensure(missile);
            Vector3 n = missile.rb != null ? missile.rb.velocity : Vector3.forward;
            AllowDetonate = true;
            try
            {
                missile.Detonate(n, false, false);
            }
            finally
            {
                AllowDetonate = false;
            }
        }

        internal static bool ImpactArmed(Missile missile)
        {
            WarewindFlight? f = missile != null ? missile.GetComponent<WarewindFlight>() : null;
            return f != null && f.Armed;
        }
    }
}
