using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// HUD dynamic range. Shared AAM2 prefab.CalcRange uses Scimitar motors — wrong for X-77.
    /// </summary>
    internal static class WarewindRange
    {
        internal static float Calc(
            float launchSpeed,
            float launchAltitude,
            float targetAltitude,
            float targetDist,
            float targetRelativeSpeed,
            out float noEscapeDistance)
        {
            // Match Missile.CalcRange shape with Warewind motor budget (not AAM2).
            float midAlt = Mathf.Lerp(launchAltitude, targetAltitude, 0.5f);
            float rho = ApproxDensity(midAlt);
            float mass = WarewindConstants.LaunchMassKg;
            float fuel = WarewindConstants.BoosterFuelKg + WarewindConstants.SustainerFuelKg;
            float burn = WarewindConstants.BoosterBurnS + WarewindConstants.SustainerBurnS;
            float dry = Mathf.Max(400f, mass - fuel);

            float boostThrust = WarewindConstants.BoosterTwr * mass * WarewindConstants.GravityMps2;
            float sustMass = Mathf.Max(800f, mass - WarewindConstants.Stage1DryMassKg - WarewindConstants.BoosterFuelKg * 0.5f);
            float sustThrust = WarewindConstants.SustainerTwr * sustMass * WarewindConstants.GravityMps2;

            float deltaV = boostThrust * WarewindConstants.BoosterBurnS / Mathf.Max(500f, mass - WarewindConstants.BoosterFuelKg * 0.5f)
                           + sustThrust * WarewindConstants.SustainerBurnS / Mathf.Max(400f, dry);

            float fin = Mathf.Max(WarewindConstants.MinFinArea, 1.8f);
            float dragCoef = 0.02f;
            float qDen = Mathf.Max(1e-5f, dragCoef * rho * 0.5f * fin);
            float cruiseTop = MachCapSpeed(midAlt);
            float eqSpeed = Mathf.Sqrt(sustThrust / qDen);
            float peak = Mathf.Min(launchSpeed + deltaV, Mathf.Max(eqSpeed, cruiseTop));

            float powered = burn < 30f
                ? Mathf.Lerp(launchSpeed, peak, 0.5f) * burn
                : peak * burn;

            float climbSlope = targetDist > 1f
                ? Mathf.Clamp((targetAltitude - launchAltitude) / targetDist, -0.5f, 0.5f)
                : -0.1f;
            float remainClimb = Mathf.Abs(launchAltitude - targetAltitude);
            float range = powered;
            float time = 0f;
            float speed = peak;
            float dt = 0.1f;
            float dragK = 0.5f * dragCoef * rho * fin / dry;
            noEscapeDistance = 0f;
            float minSpeed = 120f;

            for (int i = 0; i < 120; i++)
            {
                range += dt * speed;
                time += dt;
                if (remainClimb > 0f)
                {
                    float eKin = 0.5f * dry * speed * speed;
                    float dy = dt * climbSlope * speed;
                    remainClimb -= Mathf.Abs(dy);
                    float e = eKin + dry * -9.81f * dy;
                    speed = Mathf.Sqrt(Mathf.Max(1f, 2f * e / dry));
                }
                speed -= dt * speed * speed * dragK;
                speed = Mathf.Min(speed, cruiseTop);
                dt += 0.05f;
                if (i > 10)
                {
                    if (speed < minSpeed)
                        break;
                    if (noEscapeDistance <= 0f && speed < Mathf.Max(1f, targetRelativeSpeed))
                    {
                        noEscapeDistance = range - targetRelativeSpeed * time;
                        targetRelativeSpeed = 0f;
                    }
                }
            }

            if (noEscapeDistance <= 0f)
                noEscapeDistance = range - time * targetRelativeSpeed;

            float capped = Mathf.Clamp(range, 15000f, WarewindConstants.HudMaxRangeM);
            noEscapeDistance = Mathf.Clamp(noEscapeDistance, capped * 0.35f, capped);
            return capped;
        }

        internal static float MachCapSpeed(float altM)
        {
            float sos = ApproxSos(altM);
            float mach = MachCap(altM);
            return mach * sos;
        }

        internal static float MachCap(float altM)
        {
            float a = Mathf.Max(0f, altM);
            if (a <= WarewindConstants.Mach5BelowAltM)
                return WarewindConstants.MachLow;
            if (a >= WarewindConstants.Mach8AboveAltM)
                return WarewindConstants.MachHigh;
            float u = (a - WarewindConstants.Mach5BelowAltM) /
                      (WarewindConstants.Mach8AboveAltM - WarewindConstants.Mach5BelowAltM);
            return Mathf.Lerp(WarewindConstants.MachLow, WarewindConstants.MachHigh, u);
        }

        private static float ApproxDensity(float altM)
        {
            return WarewindConstants.AtmosphereRho0 *
                   Mathf.Exp(-Mathf.Max(0f, altM) / WarewindConstants.AtmosphereScaleH);
        }

        private static float ApproxSos(float altM)
        {
            // Rough troposphere / stratosphere SoS (m/s).
            float a = Mathf.Max(0f, altM);
            if (a < 11000f)
                return 340f - a * 0.004f;
            if (a < 20000f)
                return 295f;
            if (a < 32000f)
                return 295f + (a - 20000f) * 0.0025f;
            return Mathf.Clamp(330f + (a - 32000f) * 0.001f, 300f, 360f);
        }
    }
}
