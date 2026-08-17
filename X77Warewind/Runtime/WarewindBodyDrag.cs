using System.Reflection;
using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Body drag scaled by flight phase. Full ISA blend at sea level killed loft;
    /// loft uses light game-density drag, cruise/dive use full CdA for descent bleed.
    /// </summary>
    internal static class WarewindBodyDrag
    {
        private static readonly FieldInfo? AirDensity =
            typeof(Missile).GetField("airDensity", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Apply(Missile missile)
        {
            if (missile?.rb == null || missile.disabled)
                return;

            Vector3 vel = missile.rb.velocity;
            float speed = vel.magnitude;
            if (speed < 5f)
                return;

            float gameRho = 0f;
            if (AirDensity != null && AirDensity.GetValue(missile) is float d)
                gameRho = Mathf.Max(0f, d);

            float alt = Mathf.Max(0f, missile.transform.position.y - Datum.LocalSeaY);
            float isaRho = WarewindConstants.AtmosphereRho0 *
                           Mathf.Exp(-alt / WarewindConstants.AtmosphereScaleH);

            WarewindPhase phase = WarewindPhase.Loft;
            WarewindFlight? f = missile.GetComponent<WarewindFlight>();
            if (f != null)
                phase = f.Phase;

            float scale;
            float rho;
            switch (phase)
            {
                case WarewindPhase.Drop:
                case WarewindPhase.Align:
                case WarewindPhase.Loft:
                    // Climb: only real game density, heavily reduced — do not invent sea-level drag.
                    scale = WarewindConstants.DragLoftScale;
                    rho = gameRho;
                    break;
                case WarewindPhase.Dive:
                    scale = WarewindConstants.DragDiveScale;
                    rho = Mathf.Max(gameRho, isaRho * 0.35f);
                    break;
                default:
                    scale = WarewindConstants.DragCruiseScale;
                    rho = Mathf.Max(gameRho, isaRho * 0.25f);
                    break;
            }

            if (rho < 1e-5f || scale <= 0f)
                return;

            float q = 0.5f * rho * speed * speed;
            float drag = WarewindConstants.BodyCd * WarewindConstants.BodyAreaM2 * q * scale;
            missile.rb.AddForce(-vel / speed * drag, ForceMode.Force);
        }
    }
}
