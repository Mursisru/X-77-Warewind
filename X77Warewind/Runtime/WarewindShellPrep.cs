using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>Disable AAM donor fuse quirks until our guidance arms the round.</summary>
    internal static class WarewindShellPrep
    {
        private static readonly FieldInfo? ImpactFuse =
            typeof(Missile).GetField("impactFuse", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? ImpactFuseDelay =
            typeof(Missile).GetField("impactFuseDelay", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Apply(Missile missile)
        {
            if (missile == null)
                return;

            ImpactFuse?.SetValue(missile, false);
            ImpactFuseDelay?.SetValue(missile, 99f);
            missile.SetTangible(false);
            WarewindBlast.Ensure(missile);

            Rigidbody? rb = missile.rb != null ? missile.rb : missile.GetComponent<Rigidbody>();
            if (rb == null)
                return;
            rb.detectCollisions = false;
            rb.useGravity = false;
        }

        internal static void EnableImpact(Missile missile)
        {
            if (missile == null)
                return;
            // delay!=0 → PenetrateObject disables impactFuse and never HE-detonates.
            ImpactFuse?.SetValue(missile, true);
            ImpactFuseDelay?.SetValue(missile, 0f);
            WarewindBlast.Ensure(missile);
            Rigidbody? rb = missile.rb != null ? missile.rb : missile.GetComponent<Rigidbody>();
            if (rb != null)
                rb.detectCollisions = true;
        }
    }
}
