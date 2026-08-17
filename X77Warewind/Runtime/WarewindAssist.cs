using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Intentionally empty. Velocity rewrite caused sideways energy / speed dumps.
    /// Guidance is AimPoint-only; high-alt authority is WarewindAero thin-air torque.
    /// </summary>
    internal static class WarewindAssist
    {
        internal static void Tick(Missile missile, WarewindFlight f, Vector3 aimDir)
        {
            // no-op
        }
    }
}
