using System.Collections;
using Blueprinter;
using HarmonyLib;
using UnityEngine;

namespace Warewind.Patches
{
    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new System.Type[] { })]
    internal static class EncyclopediaAfterLoadPatch
    {
        private static void Postfix(Encyclopedia __instance)
        {
            if (__instance == null || WarewindPlugin.Instance == null)
                return;
            WarewindPlugin.Instance.StartBootstrap(__instance);
        }
    }

    /// <summary>Wait until Blueprinter finished Ops, then register torpedo.</summary>
    internal static class BlueprinterGate
    {
        internal static IEnumerator WaitUntilReady()
        {
            float timeout = 120f;
            float t = 0f;
            while (t < timeout)
            {
                Plugin? bp = Plugin.Instance;
                if (bp != null && bp.PatchingComplete)
                    yield break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            WarewindPlugin.ModLog?.LogWarning("Blueprinter PatchingComplete timeout — continuing bootstrap.");
        }
    }
}

