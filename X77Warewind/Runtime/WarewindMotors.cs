using System;
using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>Two vanilla Missile.Motor stages. Stage2 numbers scaled from AAM-36 / AAM2.</summary>
    internal static class WarewindMotors
    {
        private static readonly FieldInfo? MotorsField =
            typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? MassField =
            typeof(Missile).GetField("mass", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? BlastYieldField =
            typeof(Missile).GetField("blastYield", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? MotorStageField =
            typeof(Missile).GetField("motorStage", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? BoosterAttachedField =
            typeof(Missile).GetField("boosterIsAttached", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Type? MotorType =
            typeof(Missile).GetNestedType("Motor", BindingFlags.NonPublic | BindingFlags.Public);

        internal static void CaptureDonor(Missile? donor)
        {
            if (donor == null || MotorsField == null || MotorType == null)
                return;
            Array? motors = MotorsField.GetValue(donor) as Array;
            if (motors == null || motors.Length == 0)
                return;
            object? m = motors.GetValue(motors.Length > 1 ? motors.Length - 1 : 0);
            if (m == null)
                return;
            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind donor motor thrust={ReadFloat(m, "thrust", 0f):F0} fuel={ReadFloat(m, "fuelMass", 0f):F0} burn={ReadFloat(m, "burnTime", 0f):F0}s top={ReadFloat(m, "topSpeed", 0f):F0}");
        }

        internal static void Apply(Missile missile)
        {
            if (missile == null || MotorsField == null || MotorType == null)
                return;

            MassField?.SetValue(missile, WarewindConstants.LaunchMassKg);
            BlastYieldField?.SetValue(missile, WarewindConstants.BlastYieldKg);
            BoosterAttachedField?.SetValue(missile, false);
            if (missile.rb != null)
                missile.rb.mass = WarewindConstants.LaunchMassKg;

            Array? src = MotorsField.GetValue(missile) as Array;
            if (src == null || src.Length == 0)
                return;

            object? src0 = src.GetValue(0);
            if (src0 == null)
                return;

            Array dst = Array.CreateInstance(MotorType, 2);
            object booster = CloneMotor(src0);
            object sustain = CloneMotor(src.Length > 1 && src.GetValue(1) != null ? src.GetValue(1)! : src0);

            WriteFloat(booster, "thrust", WarewindConstants.BoosterTwr * WarewindConstants.LaunchMassKg * WarewindConstants.GravityMps2);
            WriteFloat(booster, "fuelMass", WarewindConstants.BoosterFuelKg);
            WriteFloat(booster, "burnTime", WarewindConstants.BoosterBurnS);
            WriteFloat(booster, "topSpeed", WarewindConstants.BoosterTopSpeedMps);
            WritePrivateFloat(booster, "delayTimer", WarewindConstants.MotorDelayS);

            float sustainMass = Mathf.Max(800f, WarewindConstants.LaunchMassKg - WarewindConstants.Stage1DryMassKg - WarewindConstants.BoosterFuelKg * 0.5f);
            // Fixed design TWR — never inflate from donor (vacuum runaway).
            float sThrust = WarewindConstants.SustainerTwr * sustainMass * WarewindConstants.GravityMps2;
            WriteFloat(sustain, "thrust", sThrust);
            WriteFloat(sustain, "fuelMass", WarewindConstants.SustainerFuelKg);
            WriteFloat(sustain, "burnTime", WarewindConstants.SustainerBurnS);
            WriteFloat(sustain, "topSpeed", WarewindConstants.SustainerTopSpeedMps);
            WritePrivateFloat(sustain, "delayTimer", 0f);

            dst.SetValue(booster, 0);
            dst.SetValue(sustain, 1);
            MotorsField.SetValue(missile, dst);
            MotorStageField?.SetValue(missile, 0);

            float bThrust = WarewindConstants.BoosterTwr * WarewindConstants.LaunchMassKg * WarewindConstants.GravityMps2;
            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind motors booster TWR={WarewindConstants.BoosterTwr:F1} F={bThrust:F0}N sustain TWR={WarewindConstants.SustainerTwr:F2} F={sThrust:F0}N");
        }

        internal static int MotorStage(Missile? missile)
        {
            if (missile == null || MotorStageField == null)
                return 0;
            object? v = MotorStageField.GetValue(missile);
            return v is int i ? i : 0;
        }

        internal static float StageTopSpeed(Missile? missile)
        {
            float alt = 0f;
            if (missile != null)
                alt = Mathf.Max(0f, missile.transform.position.y - Datum.LocalSeaY);

            if (MotorStage(missile) <= 0)
                return Mathf.Min(WarewindConstants.BoosterTopSpeedMps, WarewindRange.MachCapSpeed(alt));

            return Mathf.Min(WarewindConstants.SustainerTopSpeedMps, WarewindRange.MachCapSpeed(alt));
        }

        /// <summary>Hard clamp to altitude Mach budget — no vacuum runaway past M8.</summary>
        internal static void ClampSpeed(Missile missile)
        {
            if (missile?.rb == null)
                return;
            float top = StageTopSpeed(missile);
            Vector3 v = missile.rb.velocity;
            float sp = v.magnitude;
            if (sp > top && top > 1f)
                missile.rb.velocity = v * (top / sp);
        }

        internal static float Stage0Fuel(Missile missile)
        {
            if (missile == null || MotorsField == null)
                return 0f;
            Array? motors = MotorsField.GetValue(missile) as Array;
            if (motors == null || motors.Length == 0)
                return 0f;
            object? m0 = motors.GetValue(0);
            return m0 == null ? 0f : ReadFloat(m0, "fuelMass", 0f);
        }

        internal static void SubtractDryMass(Missile missile, float kg)
        {
            if (missile?.rb == null || kg <= 0f)
                return;
            missile.rb.mass = Mathf.Max(50f, missile.rb.mass - kg);
        }

        private static object CloneMotor(object src)
        {
            object dst = Activator.CreateInstance(MotorType!)!;
            FieldInfo[] fields = MotorType!.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
                fields[i].SetValue(dst, fields[i].GetValue(src));

            WriteBool(dst, "activated", false);
            WritePrivateFloat(dst, "burnRate", 0f);
            // delayTimer re-Play()s startupSource when !isPlaying — clear to stop cyclic audio.
            FieldInfo? startup = MotorType!.GetField("startupSource", BindingFlags.Instance | BindingFlags.NonPublic);
            startup?.SetValue(dst, null);
            EnsureEmptyArray(dst, "particleSystems");
            EnsureEmptyArray(dst, "trailEmitters");
            EnsureEmptyArray(dst, "audioSources");
            EnsureEmptyArray(dst, "lights");
            EnsureEmptyArray(dst, "destructEffects");
            return dst;
        }

        private static void WriteBool(object motor, string name, bool value)
        {
            FieldInfo? f = MotorType?.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool))
                f.SetValue(motor, value);
        }

        private static void EnsureEmptyArray(object motor, string name)
        {
            FieldInfo? f = MotorType?.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null || !f.FieldType.IsArray || f.GetValue(motor) != null)
                return;
            Type? el = f.FieldType.GetElementType();
            if (el == null)
                return;
            f.SetValue(motor, Array.CreateInstance(el, 0));
        }

        private static float ReadFloat(object motor, string name, float fallback)
        {
            FieldInfo? f = MotorType?.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null || f.FieldType != typeof(float))
                return fallback;
            object? v = f.GetValue(motor);
            return v is float n ? n : fallback;
        }

        private static void WriteFloat(object motor, string name, float value)
        {
            FieldInfo? f = MotorType?.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
                f.SetValue(motor, value);
        }

        private static void WritePrivateFloat(object motor, string name, float value)
        {
            FieldInfo? f = MotorType?.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(float))
                f.SetValue(motor, value);
        }
    }
}
