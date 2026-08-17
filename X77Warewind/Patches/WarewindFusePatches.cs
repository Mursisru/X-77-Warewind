using HarmonyLib;
using Warewind;
using UnityEngine;

namespace Warewind.Patches
{
    [HarmonyPatch(typeof(Missile), nameof(Missile.Arm))]
    internal static class WarewindArmBlockPatch
    {
        private static bool Prefix(Missile __instance) =>
            !WarewindBootstrap.IsOurs(__instance) || WarewindFuse.AllowArm;
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    internal static class WarewindDetonateBlockPatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (!WarewindBootstrap.IsOurs(__instance))
                return true;
            if (WarewindFuse.AllowDetonate || WarewindFuse.ImpactArmed(__instance))
            {
                WarewindBlast.Ensure(__instance);
                return true;
            }
            WarewindPlugin.ModLog?.LogWarning(
                $"Warewind Detonate blocked t={__instance.timeSinceSpawn:F2}s armed={__instance.IsArmed()}");
            return false;
        }
    }

    [HarmonyPatch(typeof(Missile.Warhead), nameof(Missile.Warhead.Detonate))]
    internal static class WarewindWarheadDetonatePatch
    {
        private static void Prefix(Rigidbody rb, ref float blastYield)
        {
            Missile? m = rb != null ? rb.GetComponent<Missile>() : null;
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return;
            WarewindBlast.Ensure(m);
            blastYield = WarewindConstants.BlastYieldKg;
        }

        private static void Postfix(
            Rigidbody rb, PersistentID ownerID, Vector3 position, bool armed, float blastYield)
        {
            if (!armed || blastYield < 200f)
                return;
            // yield>200 with no Shockwave on FX = Warhead returns without damage.
            if (!WarewindBlast.NeedsFragFallback)
                return;
            // rb belongs to our missile when Detonate is from Warewind Claim path.
            Missile? m = rb != null ? rb.GetComponent<Missile>() : null;
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return;
            WarewindBlast.FallbackBlast(m, position);
        }
    }

    [HarmonyPatch(typeof(Missile), "OnEnable")]
    internal static class WarewindOnEnableSurvivalPatch
    {
        private static void Postfix(Missile __instance)
        {
            if (WarewindBootstrap.IsOurs(__instance))
                WarewindSurvivability.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.TakeDamage))]
    internal static class WarewindTakeDamagePatch
    {
        private static bool Prefix(
            Missile __instance,
            float pierceDamage,
            float blastDamage,
            float amountAffected,
            float fireDamage,
            float impactDamage,
            PersistentID dealerID)
        {
            if (!WarewindBootstrap.IsOurs(__instance))
                return true;
            // Vanilla: any impactDamage > 0 → instant Detonate (API/flak one-shot).
            WarewindSurvivability.ProcessDamage(
                __instance, pierceDamage, blastDamage,
                Mathf.Min(amountAffected, WarewindConstants.IncomingBlastAffectedCap),
                fireDamage, dealerID);
            return false;
        }
    }

    [HarmonyPatch(typeof(Missile), "DetectCollisions")]
    internal static class WarewindDetectCollisionsPatch
    {
        private static bool Prefix(Missile __instance) =>
            !WarewindBootstrap.IsOurs(__instance) || WarewindFuse.ImpactArmed(__instance);
    }

    [HarmonyPatch(typeof(Missile), "ApplyAero")]
    internal static class WarewindApplyAeroDragPatch
    {
        private static void Postfix(Missile __instance)
        {
            if (__instance == null || !WarewindBootstrap.IsOurs(__instance))
                return;
            WarewindBodyDrag.Apply(__instance);
        }
    }
}
