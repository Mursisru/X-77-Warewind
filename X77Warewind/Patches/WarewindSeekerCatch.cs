using HarmonyLib;
using Warewind;

namespace Warewind.Patches
{
    /// <summary>
    /// ServerFixedUpdate calls the concrete Seek override. Harmony on MissileSeeker.Seek
    /// does not intercept IR/SARH/ARM/bomb. Redirect those to WarewindGuidance.
    /// </summary>
    internal static class WarewindSeekRedirect
    {
        internal static bool Prefix(MissileSeeker seeker)
        {
            Missile? m = WarewindPatchUtil.MissileOf(seeker);
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return true;
            WarewindGuidance.Tick(m);
            return false;
        }
    }

    [HarmonyPatch(typeof(IRSeeker), nameof(IRSeeker.Seek))]
    internal static class WarewindIrSeekPatch
    {
        private static bool Prefix(IRSeeker __instance) => WarewindSeekRedirect.Prefix(__instance);
    }

    [HarmonyPatch(typeof(SARHSeeker), nameof(SARHSeeker.Seek))]
    internal static class WarewindSarhSeekPatch
    {
        private static bool Prefix(SARHSeeker __instance) => WarewindSeekRedirect.Prefix(__instance);
    }

    [HarmonyPatch(typeof(ARMSeeker), nameof(ARMSeeker.Seek))]
    internal static class WarewindArmSeekPatch
    {
        private static bool Prefix(ARMSeeker __instance) => WarewindSeekRedirect.Prefix(__instance);
    }

    [HarmonyPatch(typeof(OpticalSeekerBomb), nameof(OpticalSeekerBomb.Seek))]
    internal static class WarewindBombSeekPatch
    {
        private static bool Prefix(OpticalSeekerBomb __instance) => WarewindSeekRedirect.Prefix(__instance);
    }

    [HarmonyPatch(typeof(IRSeeker), "SlowChecks")]
    internal static class WarewindIrSlowPatch
    {
        private static bool Prefix(IRSeeker __instance)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            return m == null || !WarewindBootstrap.IsOurs(m);
        }
    }

    [HarmonyPatch(typeof(SARHSeeker), "SlowChecks")]
    internal static class WarewindSarhSlowPatch
    {
        private static bool Prefix(SARHSeeker __instance)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            return m == null || !WarewindBootstrap.IsOurs(m);
        }
    }
}
