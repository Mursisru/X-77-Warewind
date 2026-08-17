using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Stage G/turn + mild thin-air torque. Heavy ThinAir×2.8 + upright fought PID → shake.
    /// </summary>
    internal static class WarewindAero
    {
        private static readonly FieldInfo? FinArea =
            typeof(Missile).GetField("finArea", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? CurrentFinArea =
            typeof(Missile).GetField("currentFinArea", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? Torque =
            typeof(Missile).GetField("torque", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? Upright =
            typeof(Missile).GetField("uprightPreference", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? GLimit =
            typeof(Missile).GetField("gLimit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? MaxTurn =
            typeof(Missile).GetField("maxTurnRate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? SupersonicDrag =
            typeof(Missile).GetField("supersonicDrag", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? AirDensity =
            typeof(Missile).GetField("airDensity", BindingFlags.Instance | BindingFlags.NonPublic);

        private static int _lastLoggedStage = -1;
        private static float _baseTorque = WarewindConstants.MinTorque;

        internal static void Apply(Missile missile)
        {
            if (missile == null)
                return;

            float fin = Read(FinArea, missile, 0.4f) * WarewindConstants.FinAreaScale;
            fin = Mathf.Max(fin, WarewindConstants.MinFinArea);
            FinArea?.SetValue(missile, fin);
            CurrentFinArea?.SetValue(missile, fin * 0.25f);

            _baseTorque = Read(Torque, missile, 8f) * WarewindConstants.TorqueScale;
            _baseTorque = Mathf.Clamp(_baseTorque, WarewindConstants.MinTorque, 14f);

            // Low upright — high values make Steering roll-hunt and shake the hull.
            Upright?.SetValue(missile, WarewindConstants.UprightPreference);
            if (SupersonicDrag != null && Read(SupersonicDrag, missile, 0f) < 0.25f)
                SupersonicDrag.SetValue(missile, 0.35f);

            ApplyStageLimits(missile, forceLog: true);
            if (missile.rb != null)
                missile.rb.angularDrag = Mathf.Max(missile.rb.angularDrag, WarewindConstants.AngularDrag);
        }

        internal static void OnFinsDeployed(Missile missile)
        {
            if (missile == null || FinArea == null || CurrentFinArea == null)
                return;
            if (FinArea.GetValue(missile) is float fa)
                CurrentFinArea.SetValue(missile, fa);
        }

        internal static void Tick(Missile missile)
        {
            if (missile == null)
                return;
            ApplyStageLimits(missile, forceLog: false);
            DampSpin(missile);
        }

        private static void ApplyStageLimits(Missile missile, bool forceLog)
        {
            int stage = WarewindMotors.MotorStage(missile);
            bool booster = stage <= 0;
            float g = booster ? WarewindConstants.GLimitStage1 : WarewindConstants.GLimitStage2;
            float turn = booster ? WarewindConstants.MaxTurnRateStage1Deg : WarewindConstants.MaxTurnRateStage2Deg;
            float torque = _baseTorque;

            float rho = 0f;
            if (AirDensity != null && AirDensity.GetValue(missile) is float d)
                rho = Mathf.Max(0f, d);

            if (rho < WarewindConstants.ThinAirRho)
            {
                float t = 1f - Mathf.Clamp01(rho / WarewindConstants.ThinAirRho);
                float mult = Mathf.Lerp(1f, WarewindConstants.ThinAirAuthorityMult, t);
                g *= mult;
                turn *= mult;
                torque *= mult;
                g = Mathf.Min(g, WarewindConstants.ThinAirGCap);
                turn = Mathf.Min(turn, WarewindConstants.ThinAirTurnCapDeg);
                torque = Mathf.Min(torque, WarewindConstants.ThinAirTorqueCap);
            }

            GLimit?.SetValue(missile, g);
            MaxTurn?.SetValue(missile, turn);
            missile.SetTorque(torque, turn);

            if (forceLog || stage != _lastLoggedStage)
            {
                _lastLoggedStage = stage;
                WarewindPlugin.ModLog?.LogInfo(
                    $"Warewind aero stage={stage} gLimit={g:F0} turn={turn:F0} torque={torque:F1} rho={rho:F3}");
            }
        }

        private static void DampSpin(Missile missile)
        {
            if (missile.rb == null)
                return;
            float turn = MaxTurn?.GetValue(missile) is float t
                ? t
                : WarewindConstants.MaxTurnRateStage1Deg;
            float maxRad = turn * Mathf.Deg2Rad * WarewindConstants.AngVelSlack;
            Vector3 w = missile.rb.angularVelocity;
            float mag = w.magnitude;
            if (mag > maxRad)
                missile.rb.angularVelocity = w * (maxRad / mag);
        }

        private static float Read(FieldInfo? f, Missile m, float fallback)
        {
            if (f == null)
                return fallback;
            object? v = f.GetValue(m);
            return v is float n ? n : fallback;
        }
    }
}
