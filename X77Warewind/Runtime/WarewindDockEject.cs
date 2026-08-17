using Warewind.Runtime;
using UnityEngine;

namespace Warewind
{
    /// <summary>Adapter ring (DockingPort mesh) ejects on launch.</summary>
    internal static class WarewindDockEject
    {
        internal static void TryEject(Missile missile, WarewindFlight flight)
        {
            if (missile == null || flight == null || flight.DockEjected)
                return;

            Transform? vis = WarewindVisualStamp.FindVisual(missile.transform);
            if (vis == null)
                return;

            Transform? dock = FindDockingPort(vis);
            if (dock == null)
            {
                WarewindPlugin.ModLog?.LogWarning("Warewind: DockingPort mesh not found under visual.");
                flight.DockEjected = true;
                return;
            }

            flight.DockEjected = true;
            Vector3 vel = missile.rb != null ? missile.rb.velocity : Vector3.zero;
            Vector3 aft = -missile.transform.forward;
            Vector3 down = Vector3.down;
            Vector3 pos = dock.position;
            Quaternion rot = dock.rotation;

            // Detach mesh + any child empties that share the name prefix.
            dock.SetParent(null, true);
            dock.position = pos;
            dock.rotation = rot;
            dock.localScale = Vector3.one;

            Rigidbody rb = dock.GetComponent<Rigidbody>();
            if (rb == null)
                rb = dock.gameObject.AddComponent<Rigidbody>();
            rb.mass = WarewindConstants.DockMassKg;
            rb.drag = 0.4f;
            rb.angularDrag = 0.1f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.velocity = vel + aft * WarewindConstants.DockEjectSpeed + down * 4f;
            rb.angularVelocity = missile.transform.right * 1.2f + missile.transform.up * 0.3f;

            Collider[] cols = dock.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    Object.Destroy(cols[i]);
            }

            Object.Destroy(dock.gameObject, WarewindConstants.DockDestroyS);
            WarewindPlugin.ModLog?.LogInfo($"Warewind DockingPort ejected '{dock.name}'.");
        }

        private static Transform? FindDockingPort(Transform vis)
        {
            Transform? exact = TransformBinder.FindByAliases(vis, WarewindConstants.DockAliases);
            if (exact != null && exact.GetComponent<MeshFilter>() != null)
                return exact;

            Transform[] all = vis.GetComponentsInChildren<Transform>(true);
            Transform? mesh = null;
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != "DockingPort")
                    continue;
                if (t.GetComponent<MeshFilter>() != null || t.GetComponent<MeshRenderer>() != null)
                    return t;
                mesh ??= t;
            }
            return mesh;
        }
    }
}
