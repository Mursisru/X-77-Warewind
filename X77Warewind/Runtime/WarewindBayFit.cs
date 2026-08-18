using System;
using UnityEngine;

namespace Warewind.Runtime
{
    /// <summary>AB-4 Alkyon central bay only — side bays keep pylon stamp (no forward inset).</summary>
    internal static class WarewindBayFit
    {
        /// <summary>AB-4 Alkyon internal bays. Other carriers keep pylon stamp / generic bay fit.</summary>
        internal static bool IsAlkyon(Aircraft aircraft)
        {
            if (aircraft?.definition is not AircraftDefinition ad)
                return false;
            return string.Equals(ad.jsonKey, WarewindConstants.CarrierAlkyon, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldRefitAlkyonCentralBay(Aircraft aircraft, Hardpoint hardpoint)
        {
            if (!IsAlkyon(aircraft) || hardpoint == null)
                return false;

            HardpointSet? set = FindHardpointSet(aircraft, hardpoint);
            if (set != null && IsCentralBayName(set.name))
                return true;

            Transform? t = hardpoint.transform;
            return t != null && IsCentralBayName(t.name);
        }

        private static HardpointSet? FindHardpointSet(Aircraft aircraft, Hardpoint hardpoint)
        {
            WeaponManager? wm = aircraft.weaponManager;
            if (wm?.hardpointSets == null)
                return null;

            foreach (HardpointSet set in wm.hardpointSets)
            {
                if (set?.hardpoints == null)
                    continue;
                foreach (Hardpoint hp in set.hardpoints)
                {
                    if (ReferenceEquals(hp, hardpoint))
                        return set;
                }
            }
            return null;
        }

        private static bool IsCentralBayName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.IndexOf("center", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("central", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("middle", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("mid bay", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return name.IndexOf("средн", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
