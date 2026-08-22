using HarmonyLib;
using Warewind.Blueprinter;
using Warewind.Bootstrap;
using Warewind.Runtime;
using Warewind;
using NuclearOption.Networking;
using UnityEngine;

namespace Warewind.Patches
{
    [HarmonyPatch(typeof(Hardpoint), nameof(Hardpoint.SpawnMount))]
    internal static class WarewindSpawnMountPatch
    {
        private static void Prefix(WeaponMount weaponMount)
        {
            if (!WarewindBootstrap.IsOurMount(weaponMount) || weaponMount.prefab == null)
                return;

            WeaponInfo? shared = WarewindBootstrap.Info ?? weaponMount.info;
            if (shared != null)
            {
                weaponMount.info = shared;
                weaponMount.sortWeapons = true;
                if (WarewindBootstrap.Definition?.unitPrefab != null)
                    shared.weaponPrefab = WarewindBootstrap.Definition.unitPrefab;
                foreach (MountedMissile mm in weaponMount.prefab.GetComponentsInChildren<MountedMissile>(true))
                {
                    if (mm != null)
                        mm.info = shared;
                }
            }

            PrefabFactory.FreezeTemplatePhysics(weaponMount.prefab);
            weaponMount.prefab.SetActive(true);
        }

        private static void Postfix(Hardpoint __instance, Aircraft aircraft, WeaponMount weaponMount, GameObject __result)
        {
            if (!WarewindBootstrap.IsOurMount(weaponMount) || __result == null)
                return;
            if (weaponMount.prefab != null)
            {
                PrefabFactory.FreezeTemplatePhysics(weaponMount.prefab);
                weaponMount.prefab.SetActive(false);
            }

            bool bay = __instance != null && __instance.bayDoors != null && __instance.bayDoors.Length > 0;
            PrefabFactory.ActivateMountedInstance(__result, internalBay: bay);
            if (!bay)
                return;
            if (WarewindBayFit.IsAlkyon(aircraft) && __instance != null)
            {
                bool inset = WarewindBayFit.ShouldRefitAlkyonCentralBay(aircraft, __instance);
                WarewindVisualStamp.RefitBay(__result, inset, WarewindConstants.AlkyonBaySinkM);
            }
            else
                WarewindVisualStamp.RefitBay(__result, false);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit) })]
    internal static class WarewindSpawnMissileGoPatch
    {
        private static void Prefix(out bool __state)
        {
            __state = WarewindSpawnGate.TryBegin();
        }

        private static void Postfix(bool __state, GameObject missile, Unit target, Missile __result)
        {
            try
            {
                if (__result == null)
                    return;
                bool rescue = !__state && WarewindSpawnGate.ShouldRescueClaim(missile);
                if (!__state && !rescue)
                    return;
                if (rescue)
                    WarewindPlugin.ModLog?.LogWarning(
                        $"Warewind rescue Claim on '{__result.name}' (AAM2 shell, pending race)");
                WarewindSpawnGate.Claim(__result, target);
                WarewindPlugin.ModLog?.LogInfo(
                    $"Warewind SpawnMissile OK '{__result.name}' pos={__result.transform.position}");
            }
            finally
            {
                if (__state)
                    WarewindSpawnGate.End();
            }
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new[] { typeof(MissileDefinition), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit) })]
    internal static class WarewindSpawnMissileDefPatch
    {
        private static void Prefix(MissileDefinition missile, out bool __state)
        {
            __state = missile != null &&
                      string.Equals(missile.jsonKey, WarewindConstants.MissileJsonKey, System.StringComparison.Ordinal);
            if (__state)
                WarewindSpawnGate.InFlight = true;
        }

        private static void Postfix(bool __state, Unit target, Missile __result)
        {
            try
            {
                if (!__state || __result == null)
                    return;
                WarewindSpawnGate.Claim(__result, target);
            }
            finally
            {
                if (__state)
                    WarewindSpawnGate.End();
            }
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissileEncyclopedia))]
    internal static class WarewindEncyclopediaPatch
    {
        private static void Prefix(MissileDefinition missile, out bool __state)
        {
            __state = missile != null &&
                      string.Equals(missile.jsonKey, WarewindConstants.MissileJsonKey, System.StringComparison.Ordinal);
            if (__state)
                WarewindSpawnGate.InFlight = true;
        }

        private static void Postfix(bool __state, Missile __result)
        {
            try
            {
                if (!__state || __result == null)
                    return;
                NobpContent.TryLoad();
                WarewindSpawnGate.Claim(__result, null);
                __result.NetworkunitName = WarewindConstants.UnitName;
            }
            finally
            {
                if (__state)
                    WarewindSpawnGate.End();
            }
        }
    }

    [HarmonyPatch(typeof(MountedMissile), nameof(MountedMissile.Fire))]
    internal static class WarewindFirePatch
    {
        private static void Prefix(MountedMissile __instance, Unit target, GlobalPosition aimpoint)
        {
            if (__instance?.info == null || !WarewindBootstrap.IsOurInfo(__instance.info))
                return;
            WarewindSpawnGate.SyncSharedInfo(__instance);
            WarewindSpawnGate.NoteFire(__instance, target, aimpoint);
            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind Fire target={(target != null ? target.name : "aim")} pending={WarewindSpawnGate.Pending} prefab={(WarewindBootstrap.Definition?.unitPrefab != null ? WarewindBootstrap.Definition.unitPrefab.name : "NULL")}");
        }
    }

    [HarmonyPatch(typeof(Missile), "StartMissile")]
    internal static class WarewindStartMissilePatch
    {
        private static void Postfix(Missile __instance)
        {
            if (WarewindBootstrap.IsOurs(__instance))
                WarewindSpawnGate.Ensure(__instance);
        }
    }

    [HarmonyPatch(typeof(Missile), "LocalStart")]
    internal static class WarewindLocalStartPatch
    {
        // Skip vanilla seeker.Initialize (ARH SlowChecks / IR LoseLock / bomb altitude fuse).
        private static bool Prefix(Missile __instance) => !WarewindBootstrap.IsOurs(__instance);

        private static void Postfix(Missile __instance)
        {
            if (WarewindBootstrap.IsOurs(__instance))
                WarewindSpawnGate.Ensure(__instance);
        }
    }

    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class WarewindOnStartClientPatch
    {
        private static void Postfix(Missile __instance)
        {
            if (WarewindBootstrap.IsOurs(__instance))
                WarewindSpawnGate.Ensure(__instance);
        }
    }

    [HarmonyPatch(typeof(MissileSeeker), nameof(MissileSeeker.Seek))]
    internal static class WarewindSeekerSeekPatch
    {
        private static bool Prefix(MissileSeeker __instance)
        {
            Missile? m = __instance != null ? __instance.GetComponent<Missile>() : null;
            if (m == null)
                m = __instance != null ? __instance.GetComponentInParent<Missile>() : null;
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return true;
            WarewindGuidance.Tick(m);
            return false;
        }
    }

    [HarmonyPatch(typeof(OpticalSeeker), nameof(OpticalSeeker.Seek))]
    internal static class WarewindOpticalSeekPatch
    {
        private static bool Prefix(OpticalSeeker __instance)
        {
            Missile? m = __instance != null ? __instance.GetComponent<Missile>() : null;
            if (m == null)
                m = __instance != null ? __instance.GetComponentInParent<Missile>() : null;
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return true;
            WarewindGuidance.Tick(m);
            return false;
        }
    }

    [HarmonyPatch(typeof(ARHSeeker), nameof(ARHSeeker.Seek))]
    internal static class WarewindArhSeekPatch
    {
        private static bool Prefix(ARHSeeker __instance)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return true;
            WarewindGuidance.Tick(m);
            return false;
        }
    }

    [HarmonyPatch(typeof(OpticalSeekerCruiseMissile), nameof(OpticalSeekerCruiseMissile.Seek))]
    internal static class WarewindCruiseSeekPatch
    {
        private static bool Prefix(OpticalSeekerCruiseMissile __instance)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return true;
            WarewindGuidance.Tick(m);
            return false;
        }
    }

    [HarmonyPatch(typeof(OpticalSeeker), "SlowChecks")]
    internal static class WarewindOpticalSlowPatch
    {
        private static bool Prefix(OpticalSeeker __instance)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            return m == null || !WarewindBootstrap.IsOurs(m);
        }
    }

    [HarmonyPatch(typeof(ARHSeeker), "SlowChecks")]
    internal static class WarewindArhSlowPatch
    {
        private static bool Prefix(ARHSeeker __instance)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            return m == null || !WarewindBootstrap.IsOurs(m);
        }
    }

    [HarmonyPatch(typeof(OpticalSeeker), nameof(OpticalSeeker.Initialize))]
    internal static class WarewindOpticalInitPatch
    {
        private static bool Prefix(OpticalSeeker __instance, Unit target, GlobalPosition aimpoint)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return true;
            WarewindSpawnGate.Ensure(m);
            m.GetComponent<WarewindFlight>()?.CaptureTarget(target, aimpoint.ToLocalPosition(), preferAim: true);
            return false;
        }
    }

    [HarmonyPatch(typeof(ARHSeeker), nameof(ARHSeeker.Initialize))]
    internal static class WarewindArhInitPatch
    {
        private static bool Prefix(ARHSeeker __instance, Unit target, GlobalPosition aimpoint)
        {
            Missile? m = WarewindPatchUtil.MissileOf(__instance);
            if (m == null || !WarewindBootstrap.IsOurs(m))
                return true;
            WarewindSpawnGate.Ensure(m);
            m.GetComponent<WarewindFlight>()?.CaptureTarget(target, aimpoint.ToLocalPosition(), preferAim: true);
            return false;
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.GetSeekerType))]
    internal static class WarewindGetSeekerTypePatch
    {
        private static bool Prefix(Missile __instance, ref string __result)
        {
            if (!WarewindBootstrap.IsOurs(__instance))
                return true;
            __result = WarewindConstants.SeekerTypeName;
            return false;
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.GetYield))]
    internal static class WarewindGetYieldPatch
    {
        private static void Postfix(Missile __instance, ref float __result)
        {
            if (WarewindBootstrap.IsOurs(__instance))
                __result = WarewindConstants.BlastYieldKg;
        }
    }

    [HarmonyPatch(typeof(MissileDefinition), nameof(MissileDefinition.GetMass))]
    internal static class WarewindDefMassPatch
    {
        private static void Postfix(MissileDefinition __instance, ref float __result)
        {
            if (__instance != null &&
                string.Equals(__instance.jsonKey, WarewindConstants.MissileJsonKey, System.StringComparison.Ordinal))
                __result = WarewindConstants.LaunchMassKg;
        }
    }

    [HarmonyPatch(typeof(Missile), nameof(Missile.GetMass))]
    internal static class WarewindGetMassPatch
    {
        private static void Postfix(Missile __instance, ref float __result)
        {
            if (WarewindBootstrap.IsOurs(__instance))
                __result = WarewindConstants.LaunchMassKg;
        }
    }

    [HarmonyPatch(typeof(AircraftSelectionMenu), nameof(AircraftSelectionMenu.DisplayInfo))]
    internal static class WarewindDisplayInfoPatch
    {
        private static void Postfix(AircraftSelectionMenu __instance, WeaponInfo weaponInfo)
        {
            if (!WarewindBootstrap.IsOurInfo(weaponInfo))
                return;
            weaponInfo.costPerRound = WarewindConstants.Cost;
            weaponInfo.blastDamage = WarewindConstants.BlastYieldKg;
            weaponInfo.massPerRound = WarewindConstants.LaunchMassKg;
            AircraftSelectionDisplay.SetTmp(__instance, "weaponSeeker", WarewindConstants.SeekerTypeName);
            AircraftSelectionDisplay.SetTmp(__instance, "weaponHE", "HE: " + UnitConverter.YieldReading(WarewindConstants.BlastYieldKg));
            AircraftSelectionDisplay.SetTmp(__instance, "weaponCost", "C: " + UnitConverter.ValueReading(WarewindConstants.Cost));
            AircraftSelectionDisplay.SetTmp(__instance, "weaponRCS", string.Format("RCS: {0}", WarewindConstants.RadarSize));
        }
    }

    [HarmonyPatch(typeof(EncyclopediaBrowser), "DisplayUnitInfo")]
    internal static class WarewindEncyclopediaDisplayPatch
    {
        private static void Postfix(EncyclopediaBrowser __instance, UnitDefinition definition)
        {
            if (definition == null ||
                !string.Equals(definition.jsonKey, WarewindConstants.MissileJsonKey, System.StringComparison.Ordinal))
                return;
            definition.value = WarewindConstants.Cost;
            definition.length = WarewindBootstrap.LengthM;
            definition.width = WarewindBootstrap.WidthM;
            definition.height = WarewindBootstrap.HeightM;
            definition.radarSize = WarewindConstants.RadarSize;
            AircraftSelectionDisplay.SetTmp(__instance, "guidance", WarewindConstants.SeekerTypeName);
            AircraftSelectionDisplay.SetTmp(__instance, "yield", UnitConverter.YieldReading(WarewindConstants.BlastYieldKg) + " TNT");
            AircraftSelectionDisplay.SetTmp(__instance, "mass", UnitConverter.WeightReading(WarewindConstants.LaunchMassKg));
            AircraftSelectionDisplay.SetTmp(__instance, "cost", UnitConverter.ValueReading(WarewindConstants.Cost));
            AircraftSelectionDisplay.SetTmp(__instance, "rcs", string.Format("{0}", WarewindConstants.RadarSize));
            Warewind.Runtime.WarewindEncyclopediaStats.ApplyMissilePanels(__instance);
        }
    }

    [HarmonyPatch(typeof(WeaponMount), nameof(WeaponMount.Initialize))]
    internal static class WarewindMountInitPatch
    {
        private static void Postfix(WeaponMount __instance)
        {
            if (!WarewindBootstrap.IsOurMount(__instance) || __instance.info == null)
                return;
            WeaponInfo info = WarewindBootstrap.Info ?? __instance.info;
            __instance.info = info;
            __instance.sortWeapons = true;
            info.weaponName = WarewindConstants.WeaponInfoName;
            info.shortName = WarewindConstants.ShortName;
            info.massPerRound = WarewindConstants.LaunchMassKg;
            info.blastDamage = WarewindConstants.BlastYieldKg;
            info.costPerRound = WarewindConstants.Cost;
            info.fireInterval = WarewindConstants.FireIntervalS;
            info.missile = true;
            info.bomb = false;
            info.glideBomb = false;
            info.overHorizon = true;
            Sprite? preview = Warewind.Runtime.WarewindWeaponIcon.Get();
            if (preview != null)
                info.weaponIcon = preview;
            Warewind.Runtime.WarewindEncyclopediaStats.ApplyTargetRequirements(info);
            if (WarewindBootstrap.Definition?.unitPrefab != null)
                info.weaponPrefab = WarewindBootstrap.Definition.unitPrefab;
            __instance.mountName = WarewindConstants.MountDisplayName;
            __instance.mass = __instance.emptyMass + WarewindConstants.LaunchMassKg;
            __instance.RCS = WarewindConstants.RadarSize;
            if (__instance.prefab != null)
            {
                foreach (MountedMissile mm in __instance.prefab.GetComponentsInChildren<MountedMissile>(true))
                {
                    if (mm != null)
                        mm.info = info;
                }
            }
        }
    }

    [HarmonyPatch(typeof(NetworkManagerNuclearOption), "RegisterPrefabs")]
    internal static class WarewindRegisterPrefabsPatch
    {
        private static void Postfix()
        {
            GameObject? fly = WarewindBootstrap.Definition?.unitPrefab;
            if (fly == null)
                return;
            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind spawn prefab '{fly.name}' (stock AAM2, no custom PrefabHash).");
        }
    }

    internal static class WarewindPatchUtil
    {
        internal static Missile? MissileOf(Component? c)
        {
            if (c == null)
                return null;
            Missile? m = c.GetComponent<Missile>();
            return m != null ? m : c.GetComponentInParent<Missile>();
        }
    }

    internal static class AircraftSelectionDisplay
    {
        internal static void SetTmp(object host, string field, string value)
        {
            System.Reflection.FieldInfo? f = host.GetType().GetField(field,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            object? tmp = f?.GetValue(host);
            if (tmp == null)
                return;
            System.Reflection.PropertyInfo? p = tmp.GetType().GetProperty("text");
            p?.SetValue(tmp, value);
        }
    }
}
