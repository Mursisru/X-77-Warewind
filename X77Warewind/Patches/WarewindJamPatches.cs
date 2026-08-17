using HarmonyLib;
using Warewind;
using UnityEngine;

namespace Warewind.Patches
{
    /// <summary>ARH rejects off-boresight jam — Warewind body never points at inbound SAM.</summary>
    [HarmonyPatch(typeof(ARHSeeker), "ARHSeeker_OnJam")]
    internal static class WarewindArhJamBypassPatch
    {
        private static bool Prefix(ARHSeeker __instance, Unit.JamEventArgs e)
        {
            if (!(e.jammingUnit is Missile jammer) || !WarewindBootstrap.IsOurs(jammer))
                return true;
            WarewindJamInject.ApplyArh(__instance, e, e.jamAmount);
            return false;
        }
    }
}
