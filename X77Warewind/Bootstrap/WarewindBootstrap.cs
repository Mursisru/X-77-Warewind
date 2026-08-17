using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Warewind.Blueprinter;
using Warewind.Bootstrap;
using Warewind.Patches;
using Warewind.Runtime;
using UnityEngine;

namespace Warewind
{
    /// <summary>CreateInstance encyclopedia entries. Shared AAM2 unitPrefab (Hydra contract). Add-only Piledriver HE slots.</summary>
    internal static class WarewindBootstrap
    {
        private static bool _done;
        internal static MissileDefinition? Definition { get; private set; }
        internal static WeaponMount? Mount { get; private set; }
        internal static WeaponInfo? Info { get; private set; }
        internal static float LengthM = WarewindConstants.FallbackLengthM;
        internal static float WidthM = WarewindConstants.FallbackWidthM;
        internal static float HeightM = WarewindConstants.FallbackHeightM;

        private static readonly FieldInfo? UnitDisabled =
            typeof(UnitDefinition).GetField("disabled", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? MountDisabled =
            typeof(WeaponMount).GetField("disabled", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static IEnumerator Run(Encyclopedia enc)
        {
            if (_done || enc == null)
                yield break;

            yield return BlueprinterGate.WaitUntilReady();

            try
            {
                PrefabFactory.AssertPiledriverIntact(enc);
                NobpContent.TryLoad();
                WarewindFlares.Cache(enc);
                WarewindEw.Cache(enc);
                WarewindMotorFx.CaptureTbm(enc);
                WarewindBlast.CaptureTbm(enc);

                MissileDefinition? aam = ResolveAam(enc);
                if (aam?.unitPrefab != null)
                    VisualMaterials.PrimeShaderFrom(aam.unitPrefab);

                if (Encyclopedia.Lookup != null &&
                    Encyclopedia.Lookup.TryGetValue(WarewindConstants.MissileJsonKey, out UnitDefinition existing) &&
                    existing is MissileDefinition md && md.unitPrefab != null)
                {
                    Definition = md;
                    GameObject? shellGo = WarewindFlyFactory.BindSharedShell(aam ?? md);
                    if (shellGo != null)
                        md.unitPrefab = shellGo;
                    ApplyMeasuredSizeFromVisual(md);
                    WarewindSurvivability.ApplyDefinition(md);
                }
                else
                    Definition = CreateDefinition(enc, aam);

                Mk54DefinitionMass.Apply(Definition, WarewindConstants.LaunchMassKg);

                if (Encyclopedia.WeaponLookup != null &&
                    Encyclopedia.WeaponLookup.TryGetValue(WarewindConstants.MountJsonKey, out WeaponMount existingMount) &&
                    existingMount.prefab != null &&
                    !IsVanillaKey(existingMount.jsonKey))
                {
                    Mount = existingMount;
                    RefreshMount(enc, Mount, Definition);
                }
                else
                    Mount = CreateMount(enc, Definition);

                Info = Mount?.info;
                if (Mount != null && Definition?.unitPrefab != null && Mount.info != null)
                    Mount.info.weaponPrefab = Definition.unitPrefab;

                if (Mount != null)
                    HardpointInjector.InjectPiledriverSlots(enc, Mount);

                PrefabFactory.AssertPiledriverIntact(enc);
                _done = Definition != null && Mount != null;
                WarewindPlugin.ModLog?.LogInfo(_done
                    ? $"Warewind ready def={WarewindConstants.MissileJsonKey} visual={(NobpContent.WarewindVisual != null)}"
                    : "Warewind bootstrap incomplete.");
            }
            catch (Exception ex)
            {
                WarewindPlugin.ModLog?.LogError($"WarewindBootstrap: {ex}");
            }
        }

        internal static bool IsOurs(Missile? missile)
        {
            if (missile == null)
                return false;
            if (missile.GetComponent<WarewindTag>() != null)
                return true;
            // Hydra: InFlight covers LocalStart/StartMissile BEFORE Claim stamps Tag.
            // Without this, AAM2 ARH/IR Initialize+TakeDamage run as a vanilla Scimitar and boom.
            if (WarewindSpawnGate.InFlight)
                return true;
            WeaponInfo? wi = missile.GetWeaponInfo();
            if (wi != null &&
                (wi.weaponName == WarewindConstants.WeaponInfoName ||
                 wi.shortName == WarewindConstants.ShortName))
                return true;
            return missile.definition != null &&
                   string.Equals(missile.definition.jsonKey, WarewindConstants.MissileJsonKey, StringComparison.Ordinal);
        }

        internal static bool IsOurMount(WeaponMount? mount)
        {
            return mount != null &&
                   string.Equals(mount.jsonKey, WarewindConstants.MountJsonKey, StringComparison.Ordinal);
        }

        internal static bool IsOurInfo(WeaponInfo? info)
        {
            return info != null &&
                   (info.weaponName == WarewindConstants.WeaponInfoName ||
                    info.shortName == WarewindConstants.ShortName);
        }

        private static MissileDefinition? CreateDefinition(Encyclopedia enc, MissileDefinition? aam)
        {
            MissileDefinition? shell = aam ?? ResolveAam(enc);
            if (shell?.unitPrefab == null)
            {
                WarewindPlugin.ModLog?.LogError("Warewind: no AAM2/shell unitPrefab.");
                return null;
            }

            MissileDefinition def = ScriptableObject.CreateInstance<MissileDefinition>();
            def.name = "MissilePack_X77_Definition";
            def.jsonKey = WarewindConstants.MissileJsonKey;
            PrefabFactory.CopyUnitDefScalars(shell, def);
            PrefabFactory.CopyMapIdentity(shell, def);
            def.unitName = WarewindConstants.UnitName;
            def.bogeyName = WarewindConstants.BogeyName;
            def.description = "Two-stage air-launched hypersonic. Solid booster, ramjet sustainer, 700kg HE, optical.";
            def.value = WarewindConstants.Cost;
            def.mass = WarewindConstants.LaunchMassKg;
            def.length = LengthM;
            def.width = WidthM;
            def.height = HeightM;
            def.radarSize = WarewindConstants.RadarSize;
            def.code = "MSL";
            def.IsObstacle = false;
            WarewindSurvivability.ApplyDefinition(def);
            UnitDisabled?.SetValue(def, false);

            // Hydra/Yashma: reuse already-registered vanilla prefab. Do not clone NI.
            GameObject? fly = WarewindFlyFactory.BindSharedShell(shell);
            if (fly == null)
            {
                WarewindPlugin.ModLog?.LogError("Warewind: fly prefab bind failed.");
                return null;
            }
            def.unitPrefab = fly;
            ApplyMeasuredSizeFromVisual(def);

            enc.missiles ??= new List<MissileDefinition>();
            if (!enc.missiles.Contains(def))
                enc.missiles.Add(def);
            Encyclopedia.Lookup ??= new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);
            Encyclopedia.Lookup[def.jsonKey] = def;
            if (enc.IndexLookup != null && !ContainsNet(enc.IndexLookup, def))
            {
                enc.IndexLookup.Add(def);
                ((INetworkDefinition)def).LookupIndex = enc.IndexLookup.Count - 1;
            }

            Mk54DefinitionMass.Apply(def, WarewindConstants.LaunchMassKg);
            WarewindPlugin.ModLog?.LogInfo($"Created Warewind definition from shell '{shell.jsonKey}'.");
            return def;
        }

        private static void ApplyMeasuredSizeFromVisual(MissileDefinition def)
        {
            NobpContent.TryLoad();
            if (NobpContent.WarewindVisual == null || def == null)
                return;
            if (!WarewindVisualStamp.TryMeasurePrefab(NobpContent.WarewindVisual, out Vector3 size))
                return;
            LengthM = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * WarewindConstants.VisualScaleMult;
            WidthM = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * WarewindConstants.VisualScaleMult;
            if (WidthM < 0.05f)
                WidthM = WarewindConstants.FallbackWidthM;
            HeightM = WidthM;
            def.length = LengthM;
            def.width = WidthM;
            def.height = HeightM;
        }

        private static void RefreshMount(Encyclopedia enc, WeaponMount mount, MissileDefinition? def)
        {
            NobpContent.TryLoad();
            if (mount.prefab != null && NobpContent.WarewindVisual != null)
                WarewindVisualStamp.Stamp(mount.prefab, NobpContent.WarewindVisual, live: false);

            WeaponInfo info = mount.info ?? ScriptableObject.CreateInstance<WeaponInfo>();
            FillInfo(info, enc, def);
            mount.info = info;
            mount.mountName = WarewindConstants.MountDisplayName;
            mount.jsonKey = WarewindConstants.MountJsonKey;
            mount.mass = mount.emptyMass + WarewindConstants.LaunchMassKg;
            mount.RCS = WarewindConstants.RadarSize;
            mount.emptyRCS = 0f;
            mount.emptyCost = 0f;
            mount.GearSafety = true;
            mount.GroundSafety = true;
            MountDisabled?.SetValue(mount, false);
            BindMountedInfo(mount, info);
            Info = info;
        }

        private static WeaponMount? CreateMount(Encyclopedia enc, MissileDefinition? def)
        {
            if (def?.unitPrefab == null)
                return null;
            WeaponMount? donor = PrefabFactory.FindMountByExactKey(enc, WarewindConstants.MountDonorKey);
            if (donor?.prefab == null)
                donor = PrefabFactory.FindMountByExactKey(enc, WarewindConstants.MountDonorKeyAlt);
            if (donor?.prefab == null || donor.info == null)
            {
                WarewindPlugin.ModLog?.LogError("Warewind: no Piledriver HE mount donor GO.");
                return null;
            }

            string donorKey = donor.jsonKey;
            WeaponMount mount = ScriptableObject.CreateInstance<WeaponMount>();
            mount.name = "MissilePack_X77_Mount";
            mount.jsonKey = WarewindConstants.MountJsonKey;
            mount.mountName = WarewindConstants.MountDisplayName;
            PrefabFactory.CopyMountScalars(donor, mount);
            mount.ammo = 1;
            mount.emptyMass = WarewindConstants.MountEmptyMassKg;
            mount.mass = mount.emptyMass + WarewindConstants.LaunchMassKg;
            mount.RCS = WarewindConstants.RadarSize;
            mount.emptyRCS = 0f;
            mount.emptyCost = 0f;
            mount.GearSafety = true;
            mount.GroundSafety = true;
            MountDisabled?.SetValue(mount, false);

            WeaponInfo info = ScriptableObject.CreateInstance<WeaponInfo>();
            info.name = "MissilePack_X77_Info";
            FillInfo(info, enc, def);
            mount.info = info;

            GameObject mountGo = PrefabFactory.CloneAsPrefab(donor.prefab, "MissilePack_X77_MountPrefab");
            KeepSingle(mountGo);
            ForceDownRail(mountGo);
            WarewindVisualStamp.Stamp(mountGo, NobpContent.WarewindVisual, live: false);
            mount.prefab = mountGo;
            BindMountedInfo(mount, info);

            if (!string.Equals(donor.jsonKey, donorKey, StringComparison.Ordinal))
                WarewindPlugin.ModLog?.LogError($"Piledriver mount donor mutated: {donor.jsonKey}");

            enc.weaponMounts ??= new List<WeaponMount>();
            if (!enc.weaponMounts.Contains(mount))
                enc.weaponMounts.Add(mount);
            Encyclopedia.WeaponLookup ??= new Dictionary<string, WeaponMount>(StringComparer.Ordinal);
            Encyclopedia.WeaponLookup[mount.jsonKey] = mount;
            if (enc.IndexLookup != null && !ContainsNet(enc.IndexLookup, mount))
            {
                enc.IndexLookup.Add(mount);
                ((INetworkDefinition)mount).LookupIndex = enc.IndexLookup.Count - 1;
            }

            try { mount.Initialize(); }
            catch (Exception ex) { WarewindPlugin.ModLog?.LogWarning($"Warewind Initialize: {ex.Message}"); }

            mount.info = info;
            mount.mountName = WarewindConstants.MountDisplayName;
            mount.jsonKey = WarewindConstants.MountJsonKey;
            mount.GearSafety = true;
            mount.GroundSafety = true;
            info.weaponPrefab = def.unitPrefab;
            info.blastDamage = WarewindConstants.BlastYieldKg;
            Info = info;
            return mount;
        }

        private static void FillInfo(WeaponInfo info, Encyclopedia enc, MissileDefinition? def)
        {
            WeaponInfo? aamInfo = FindAamInfo(enc);
            if (aamInfo != null)
            {
                info.effectiveness = aamInfo.effectiveness;
                info.targetRequirements = aamInfo.targetRequirements;
                info.pK = aamInfo.pK;
                info.fireInterval = aamInfo.fireInterval;
                info.muzzleVelocity = aamInfo.muzzleVelocity;
                info.maxSpeed = aamInfo.maxSpeed;
                info.dragCoef = aamInfo.dragCoef;
                info.gravMult = aamInfo.gravMult;
                info.pierceDamage = aamInfo.pierceDamage;
                info.weaponIcon = aamInfo.weaponIcon;
                info.armorTierEffectiveness = aamInfo.armorTierEffectiveness;
                info.visibilityWhenFired = aamInfo.visibilityWhenFired;
                info.useWeaponDoors = aamInfo.useWeaponDoors;
                info.boresight = aamInfo.boresight;
                info.rearmGround = aamInfo.rearmGround;
                info.rearmShip = aamInfo.rearmShip;
            }

            TargetRequirements tr = info.targetRequirements;
            tr.maxRange = WarewindConstants.HudMaxRangeM;
            tr.minAltitude = -200f;
            tr.maxAltitude = 80000f;
            info.targetRequirements = tr;

            info.weaponName = WarewindConstants.WeaponInfoName;
            info.shortName = WarewindConstants.ShortName;
            info.description = "Two-stage hypersonic, optical, 700kg HE.";
            info.massPerRound = WarewindConstants.LaunchMassKg;
            info.costPerRound = WarewindConstants.Cost;
            info.blastDamage = WarewindConstants.BlastYieldKg;
            info.pK = 0.7f;
            info.fireInterval = WarewindConstants.FireIntervalS;
            info.nuclear = false;
            info.strategic = false;
            info.bomb = false;
            info.glideBomb = false;
            info.missile = true;
            info.overHorizon = true;
            info.laserGuided = false;
            info.gun = false;
            info.energy = false;
            info.jammer = false;
            info.troops = false;
            info.hideInDisplay = false;
            info.cargo = false;
            info.sling = false;
            if (def?.unitPrefab != null)
                info.weaponPrefab = def.unitPrefab;
        }

        private static WeaponInfo? FindAamInfo(Encyclopedia enc)
        {
            string[] keys = { "AAM2_single", "AAM2_double", "AAM2", "AAM2_single_internal" };
            for (int i = 0; i < keys.Length; i++)
            {
                WeaponMount? m = PrefabFactory.FindMountByExactKey(enc, keys[i]);
                if (m?.info != null)
                    return m.info;
            }
            return null;
        }

        private static MissileDefinition? ResolveAam(Encyclopedia enc)
        {
            MissileDefinition? m = PrefabFactory.FindMissileByExactKey(enc, WarewindConstants.ShellMissileKey);
            if (m?.unitPrefab != null)
                return m;
            if (enc.missiles == null)
                return null;
            MissileDefinition? fallback = null;
            foreach (MissileDefinition cand in enc.missiles)
            {
                if (cand?.unitPrefab == null || string.IsNullOrEmpty(cand.jsonKey))
                    continue;
                if (cand.jsonKey.StartsWith("BallisticMissile", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (cand.jsonKey.IndexOf("tacNuke", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (cand.jsonKey.IndexOf("AAM2", StringComparison.OrdinalIgnoreCase) >= 0)
                    return cand;
                if (fallback == null && cand.jsonKey.IndexOf("AAM", StringComparison.OrdinalIgnoreCase) >= 0)
                    fallback = cand;
            }
            return fallback;
        }

        private static void KeepSingle(GameObject mountGo)
        {
            MountedMissile[] mounted = mountGo.GetComponentsInChildren<MountedMissile>(true);
            for (int i = 1; i < mounted.Length; i++)
            {
                if (mounted[i] != null)
                    UnityEngine.Object.DestroyImmediate(mounted[i].gameObject);
            }
        }

        private static void ForceDownRail(GameObject mountGo)
        {
            MountedMissile[] mounted = mountGo.GetComponentsInChildren<MountedMissile>(true);
            foreach (MountedMissile mm in mounted)
            {
                if (mm == null)
                    continue;
                FieldInfo? railDir = typeof(MountedMissile).GetField("railDirection", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? railLen = typeof(MountedMissile).GetField("railLength", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? railSpd = typeof(MountedMissile).GetField("railSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo? railDelay = typeof(MountedMissile).GetField("railDelay", BindingFlags.Instance | BindingFlags.NonPublic);
                railDir?.SetValue(mm, MountedMissile.RailDirection.Down);
                if (railLen != null && railLen.GetValue(mm) is float len && len < 0.05f)
                    railLen.SetValue(mm, 0.8f);
                if (railSpd != null && railSpd.GetValue(mm) is float spd && spd < 0.05f)
                    railSpd.SetValue(mm, 4f);
                if (railDelay != null && railDelay.GetValue(mm) is float d && d > 2f)
                    railDelay.SetValue(mm, 0.15f);
            }
        }

        private static void BindMountedInfo(WeaponMount mount, WeaponInfo info)
        {
            if (mount.prefab == null)
                return;
            foreach (MountedMissile mm in mount.prefab.GetComponentsInChildren<MountedMissile>(true))
            {
                if (mm != null)
                    mm.info = info;
            }
        }

        private static bool IsVanillaKey(string? key)
        {
            return !string.IsNullOrEmpty(key) &&
                   key!.StartsWith("BallisticMissile1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsNet(List<INetworkDefinition> list, INetworkDefinition item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return true;
            }
            return false;
        }
    }
}
