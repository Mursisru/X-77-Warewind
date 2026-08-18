using UnityEngine;

namespace Warewind
{
    /// <summary>Altitude Mach cap helpers (flight physics).</summary>
    internal static class WarewindRange
    {
        internal static float MachCapSpeed(float altM) => MachCap(altM) * ApproxSos(altM);

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

        private static float ApproxSos(float altM)
        {
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
