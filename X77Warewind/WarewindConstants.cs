using UnityEngine;

namespace Warewind
{
    /// <summary>X-77 Warewind identity and flight tunables. json keys are stable.</summary>
    internal static class WarewindConstants
    {
        public const string MissileJsonKey = "missilepack_x77_warewind";
        public const string MountJsonKey = "MissilePack_X77_Warewind_single";
        public const string WeaponInfoName = "X-77 Warewind";
        public const string MountDisplayName = "X-77 Warewind";
        public const string UnitName = "X-77 Warewind";
        public const string ShortName = "X-77";
        public const string BogeyName = "Warewind";
        public const string SeekerTypeName = "Optical";
        public const string VisualRootName = "WarewindVisual";
        public const string FlyPrefabName = "MissilePack_Warewind_Fly";
        public const string MeshPrefabAsset = "WarewindVisual";
        public const string BundleModName = "X77Warewind";
        public const string NobpFileName = "X77Warewind.nobp";
        public const string CarrierDarkreach = "Darkreach";
        public const string CarrierAlkyon = "FastBomber1";
        public const string PiledriverNukeToken = "tacNuke";
        public const float BayBottomSlackM = 0.12f;
        public const float BayCenterLiftExtraM = 0.15f;
        public const float BayAftInsetM = 0.35f;
        public const float BayVisualScaleMult = 0.72f;

        public const string ShellMissileKey = "AAM2";
        public const string ShellMissileKeyAlt = "AAM2_single";
        public const string MountDonorKey = "BallisticMissile1_single";
        public const string MountDonorKeyAlt = "BallisticMissile1_internalx2";

        public const float LaunchMassKg = 2800f;
        public const float Stage1DryMassKg = 420f;
        public const float BlastYieldKg = 700f;
        public const float Cost = 44f;
        public const float RadarSize = 0.5f;
        public const float MountClearanceM = 0.05f;
        public const float MountEmptyMassKg = 40f;
        public const float VisualScaleMult = 0.8f;

        public const float FallbackLengthM = 8f;
        public const float FallbackWidthM = 1.2f;
        public const float FallbackHeightM = 1.2f;

        /// <summary>Free-fall before motor — Drop ends on fall OR MotorDelayS.</summary>
        public const float DropFallM = 25f;
        public const float StabilizeSeconds = 0.35f;
        public const float MotorDelayS = 2.2f;
        public const float AlignPhaseS = 2.5f;
        public const float PartialThrottle = 0.6f;
        public const float FullThrottle = 1f;
        /// <summary>Vanilla WeaponStation gap between successive station fires.</summary>
        public const float FireIntervalS = 1.5f;

        public const float CruiseAltMaxM = 50000f;
        public const float CruiseAltMinM = 1500f;
        public const float DiveCommitDistMinM = 12000f;
        public const float DiveCommitDistMaxM = 70000f;
        public const float DiveAngleMinDeg = 45f;
        public const float DiveAngleMaxDeg = 60f;
        public const float DivePullLeadM = 8000f;
        public const float ShallowLoftRangeM = 35000f;
        public const float LoftPitchMaxDeg = 48f;
        public const float LoftPitchShallowDeg = 18f;
        /// <summary>Target astern on early phases — pitch up through the vertical toward tgt azimuth.</summary>
        public const float OverTopPitchDeg = 68f;
        public const float OverTopAimMaxOffDeg = 78f;
        public const float TargetBehindDot = 0.15f;
        public const float DropPitchDeg = 12f;
        public const float AimLookaheadM = 10000f;
        public const float TerminalDirectDistM = 8000f;
        public const float SoftKillTimeoutS = 900f;
        public const float CruiseThrottle = 0.65f;
        public const float LevelBandM = 2500f;
        public const float LevelVyGain = 0.18f;
        public const float LevelClimbDamp = 0.55f;
        public const float CruisePitchMaxDeg = 10f;
        public const float PitchSlewLoftDegS = 6f;
        public const float PitchSlewCruiseDegS = 4f;
        public const float PitchSlewCatchDegS = 20f;
        public const float CruiseYawSlewDegS = 1.0f;
        public const float AimMaxOffMidDeg = 35f;
        public const float AimMaxOffCruiseDeg = 8f;
        public const float AimMaxOffDiveDeg = 62f;
        public const float TvcAltM = 18000f;
        public const float TvcBodyRateDegS = 14f;
        public const float TvcTorque = 14f;
        /// <summary>At M6 vanilla a_perp/v ≈ 0.3°/s — boost so vel follows nose while thrusting.</summary>
        public const float CrossThrustMinOffDeg = 3f;
        public const float CrossThrustFullOffDeg = 18f;
        public const float CrossThrustMaxDegS = 18f;

        public const float BoosterTwr = 3.0f;
        public const float BoosterFuelKg = 720f;
        public const float BoosterBurnS = 40f;
        /// <summary>~Mach 5 sea-level SoS budget for stage-1 / dense air.</summary>
        public const float BoosterTopSpeedMps = 1700f;
        /// <summary>Stage-2 needs real TWR in thin air to reach Mach 8.</summary>
        public const float SustainerTwr = 2.4f;
        public const float SustainerFuelKg = 320f;
        public const float SustainerBurnS = 110f;
        /// <summary>~Mach 8 ceiling; ClampSpeed uses altitude MachCap.</summary>
        public const float SustainerTopSpeedMps = 2800f;
        public const float GravityMps2 = 9.81f;
        public const float MachLow = 5f;
        public const float MachHigh = 8f;
        public const float Mach5BelowAltM = 12000f;
        public const float Mach8AboveAltM = 35000f;
        /// <summary>Design max range — HUD + encyclopedia via CalcProxy tune.</summary>
        public const float DesignRangeM = 450000f;
        public const float HudFallbackRangeM = DesignRangeM;
        public const float EncyclopediaMinRangeM = 15000f;
        public const float TotalBurnS = BoosterBurnS + SustainerBurnS;
        /// <summary>Typical bomber launch for CalcRange / encyclopedia display.</summary>
        public const float CalcRefLaunchSpeedMps = 250f;
        public const float CalcRefLaunchAltM = 10000f;
        public const float CalcRefTargetAltM = 0f;
        public const float CalcRefTargetDistM = 100000f;

        public const float Stage1DestroyS = 30f;
        public const float ArmAfterS = 3f;
        public const float TangibleAfterS = 2.5f;

        public const float DockMassKg = 35f;
        public const float DockEjectSpeed = 18f;
        public const float DockDestroyS = 25f;

        public const float FinAreaScale = 3.2f;
        public const float MinFinArea = 1.8f;
        public const float TorqueScale = 1.6f;
        public const float MinTorque = 7f;
        public const float UprightPreference = 0.35f;
        public const float GLimitStage1 = 10f;
        public const float GLimitStage2 = 15f;
        public const float MaxTurnRateStage1Deg = 14f;
        public const float MaxTurnRateStage2Deg = 18f;
        public const float AngularDrag = 0.65f;
        public const float AngVelSlack = 1.15f;
        /// <summary>Below this density, mild torque/G boost (keep low — high mult shakes).</summary>
        public const float ThinAirRho = 0.08f;
        public const float ThinAirAuthorityMult = 1.55f;
        public const float ThinAirGCap = 22f;
        public const float ThinAirTurnCapDeg = 24f;
        public const float ThinAirTorqueCap = 20f;
        public const float VacuumGCap = 55f;
        public const float VacuumTurnCapDeg = 28f;

        public const float BodyCd = 0.28f;
        public const float BodyAreaM2 = 0.95f;
        public const float AtmosphereScaleH = 7500f;
        public const float AtmosphereRho0 = 1.225f;
        public const float DragLoftScale = 0.2f;
        public const float DragCruiseScale = 1f;
        public const float DragDiveScale = 1.15f;

        public const float FxWorldScaleM = 0.85f;
        public const float FxAftNudgeM = 0.35f;
        public const float FxMaxStartSize = 1.6f;

        public const float FlareRangeM = 15000f;
        public const int FlareCount = 50;
        public const float FlareIntervalS = 0.4f;
        public const float FlareEjectSpeed = 25f;

        public const float CapacitorMax = 450f;
        public const float CapacitorRegenPerS = 35f;
        public const float JamDrawPerS = 90f;
        public const float JamPerSecond = 5.5f;
        public const float JamAntennaSlewDegS = 240f;
        public const string EwAntennaName = "PlaceOfEW";

        public const float ThreatDetectRangeM = 55000f;
        public const float ThreatAimConeDeg = 55f;
        public const float ThreatClosingDotMin = 0.35f;

        public const float BodyArmorTier = 4f;
        public const float BodyHitpoints = 12000f;
        public const float BodyPierceArmor = 520f;
        public const float BodyBlastArmor = 750f;
        public const float BodyFireArmor = 120f;
        public const float BodyPierceTolerance = 12f;
        public const float BodyBlastTolerance = 10f;
        public const float BodyFireTolerance = 8f;
        public const float IncomingDamageScale = 0.25f;
        public const float IncomingBlastAffectedCap = 0.35f;

        public const float SamAvoidUntilM = 15000f;
        public const float SamMaxDetourM = 5000f;
        public const float SamRefreshS = 2f;

        public static readonly string[] AttachPylonAliases =
        {
            "PlaceOfRocketLock", "Attach_Pylon", "Pylon", "Mount", "Hardpoint"
        };
        public static readonly string[] DockAliases =
        {
            "DockingPort"
        };
        public static readonly string[] Stage1Aliases =
        {
            "Stage1", "Booster", "BoosterStage", "FirstStage", "Stage_1", "SolidBooster"
        };
        public static readonly string[] Stage2Aliases =
        {
            "Stage2", "MainRocket", "WarewindMain", "SecondStage", "Stage_2", "Waverider"
        };
        public static readonly string[] Engine1Aliases =
        {
            "PlaceOfSpawnEngineEffectsAndLight", "Engine1", "Exhaust", "Nozzle",
            "PlaceOfEngine", "BoosterNozzle", "PlaceOfEngine1"
        };
        public static readonly string[] Engine2Aliases =
        {
            "PlaceOfSpawnEngineEffectsAndLights(secondStage)", "PlaceOfSpawnEngineEffectsAndLights",
            "Engine2", "Scramjet", "PlaceOfScramjet", "PlaceOfEngine2", "Ramjet"
        };
        public static readonly string[] FlareAliases =
        {
            "Flare", "PlaceOfFlare", "FlareEject", "CM_Flare"
        };
        public static readonly string[] EwAliases =
        {
            "Jammer", "PlaceOfEW", "Antenna", "REB"
        };
    }
}

