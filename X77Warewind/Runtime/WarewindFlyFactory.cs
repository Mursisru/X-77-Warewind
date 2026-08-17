using UnityEngine;

namespace Warewind
{
    /// <summary>
    /// Spawn contract = Hydra/Yashma: share the encyclopedia AAM2 unitPrefab.
    /// Never CloneAsPrefab+DontDestroyOnLoad a NetworkIdentity — Mirage treats that as a
    /// scene object (NetID 0 already spawned) and Destroy() the same frame.
    /// Unique PrefabHash is also forbidden: game RegisterPrefabs never saw it.
    /// Visual is stamped on the LIVE instance after Spawn returns.
    /// </summary>
    internal static class WarewindFlyFactory
    {
        internal static GameObject? BindSharedShell(MissileDefinition? aamDef)
        {
            if (aamDef?.unitPrefab == null)
            {
                WarewindPlugin.ModLog?.LogError("Warewind: no AAM2 unitPrefab to share.");
                return null;
            }

            Missile? mis = aamDef.unitPrefab.GetComponent<Missile>() ??
                           aamDef.unitPrefab.GetComponentInChildren<Missile>(true);
            WarewindMotors.CaptureDonor(mis);
            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind uses stock unitPrefab '{aamDef.unitPrefab.name}' jsonKey={aamDef.jsonKey} (no custom PrefabHash).");
            return aamDef.unitPrefab;
        }
    }
}
