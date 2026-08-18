using System;
using UnityEngine;

namespace Warewind.Runtime
{
    /// <summary>Alkyon AB-4 central bay only — not all internal bays.</summary>
    internal static class WarewindBayFit
    {
        internal static bool ShouldRefitAlkyonCentralBay(Aircraft aircraft, Hardpoint hardpoint)
        {
            if (aircraft == null || hardpoint == null)
                return false;
            if (aircraft.definition is not AircraftDefinition ad)
                return false;
            if (!string.Equals(ad.jsonKey, WarewindConstants.CarrierAlkyon, StringComparison.OrdinalIgnoreCase))
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
