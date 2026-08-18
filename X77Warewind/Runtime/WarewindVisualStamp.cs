using Mirage;
using Warewind.Bootstrap;
using Warewind.Runtime;
using UnityEngine;

namespace Warewind
{
    /// <summary>Stamp WarewindVisual (FBX size). Materials 1:1 from Blender via ApplyFbxLook.</summary>
    internal static class WarewindVisualStamp
    {
        internal static Transform? FindVisual(Transform root)
        {
            if (root == null)
                return null;
            Transform direct = root.Find(WarewindConstants.VisualRootName);
            if (direct != null)
                return direct;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == WarewindConstants.VisualRootName)
                    return all[i];
            }
            return null;
        }

        internal static void Stamp(GameObject host, GameObject? visualPrefab, bool live)
        {
            if (host == null || visualPrefab == null)
                return;

            DestroyExisting(host);
            Transform parent = PrefabFactory.ResolveVisualParent(host);
            GameObject vis = Object.Instantiate(visualPrefab, parent, false);
            vis.name = WarewindConstants.VisualRootName;
            vis.hideFlags = HideFlags.None;
            vis.SetActive(true);

            VisualMaterials.StripSceneJunk(vis);
            StripVisualPhysics(vis);
            VisualMaterials.MatchHostDrawState(vis, host);

            int visOn = 0;
            foreach (Renderer r in vis.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                r.enabled = true;
                visOn++;
            }

            if (visOn > 0)
                PrefabFactory.HideStockRenderers(host);

            VisualFit.ApplyKeepFbxSize(
                vis.transform,
                VisualMountSnap.PylonAttach,
                WarewindConstants.AttachPylonAliases,
                WarewindConstants.MountClearanceM,
                WarewindConstants.VisualScaleMult,
                WarewindConstants.Stage1Aliases,
                WarewindConstants.Stage2Aliases);
            VisualMaterials.ApplyFbxLook(vis);

            if (!live)
            {
                PrefabFactory.ResetPrefabTransform(host);
                host.SetActive(false);
                NetworkPrefabPrep.PrepareTemplate(host);
            }

            WarewindPlugin.ModLog?.LogInfo(
                $"Warewind stamp host='{host.name}' parent='{parent.name}' renderers={visOn} live={live}");
        }

        internal static void RefitBay(GameObject host)
        {
            Transform? vis = FindVisual(host.transform);
            if (vis == null)
                return;
            VisualFit.ApplyKeepFbxSize(
                vis,
                VisualMountSnap.FullModelCenter,
                WarewindConstants.AttachPylonAliases,
                0f,
                WarewindConstants.BayVisualScaleMult,
                WarewindConstants.Stage1Aliases,
                WarewindConstants.Stage2Aliases);
            VisualMaterials.ApplyFbxLook(vis.gameObject);
        }

        internal static bool TryMeasurePrefab(GameObject visualPrefab, out Vector3 size)
        {
            size = new Vector3(
                WarewindConstants.FallbackLengthM,
                WarewindConstants.FallbackHeightM,
                WarewindConstants.FallbackWidthM);
            if (visualPrefab == null)
                return false;

            GameObject tmp = Object.Instantiate(visualPrefab);
            tmp.name = "WarewindMeasureTmp";
            tmp.SetActive(true);
            bool ok = TryMeasure(tmp, out size);
            if (!ok)
            {
                Renderer[] rs = tmp.GetComponentsInChildren<Renderer>(true);
                Bounds? b = null;
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i] == null)
                        continue;
                    if (b == null)
                        b = rs[i].bounds;
                    else
                    {
                        Bounds nb = b.Value;
                        nb.Encapsulate(rs[i].bounds);
                        b = nb;
                    }
                }
                if (b.HasValue)
                {
                    size = b.Value.size;
                    ok = true;
                }
            }
            Object.DestroyImmediate(tmp);
            return ok;
        }

        internal static bool TryMeasure(GameObject host, out Vector3 size)
        {
            size = new Vector3(
                WarewindConstants.FallbackLengthM,
                WarewindConstants.FallbackHeightM,
                WarewindConstants.FallbackWidthM);
            Transform? vis = FindVisual(host.transform);
            if (vis == null)
                return false;
            Renderer[] rs = vis.GetComponentsInChildren<Renderer>(true);
            Bounds? b = null;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null)
                    continue;
                if (b == null)
                    b = rs[i].bounds;
                else
                {
                    Bounds nb = b.Value;
                    nb.Encapsulate(rs[i].bounds);
                    b = nb;
                }
            }
            if (!b.HasValue)
                return false;
            size = b.Value.size;
            return true;
        }

        // FBX colliders overlapping the launcher → TakeDamage(impact) → Detonate same frame.
        // Child NetworkIdentity on a live spawned missile → Mirage already-spawned destroy.
        private static void StripVisualPhysics(GameObject vis)
        {
            Collider[] cols = vis.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    Object.DestroyImmediate(cols[i]);
            }

            NetworkIdentity[] ids = vis.GetComponentsInChildren<NetworkIdentity>(true);
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] != null)
                    Object.DestroyImmediate(ids[i]);
            }
        }

        private static void DestroyExisting(GameObject host)
        {
            Transform[] all = host.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                if (all[i] != null && all[i].name == WarewindConstants.VisualRootName)
                    Object.DestroyImmediate(all[i].gameObject);
            }
        }
    }
}
