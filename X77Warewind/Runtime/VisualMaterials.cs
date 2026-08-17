using UnityEngine;

namespace Warewind.Runtime
{
    /// <summary>Paint WarewindVisual from baked FBX (tint, metallic, smoothness, maps). Cull Off.</summary>
    internal static class VisualMaterials
    {
        internal static void StripSceneJunk(GameObject root)
        {
            if (root == null)
                return;

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light != null)
                    UnityEngine.Object.DestroyImmediate(light.gameObject);
            }

            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam != null)
                    UnityEngine.Object.DestroyImmediate(cam.gameObject);
            }
        }

        internal static void PrimeShaderFrom(GameObject? sampleRoot) => VisualShader.PrimeFrom(sampleRoot);


        /// <summary>
        /// Warewind: URP Lit 1:1 from baked FBX (tint, metallic, smoothness, maps). Cull Off = Blender.
        /// </summary>
        internal static void ApplyFbxLook(GameObject root)
        {
            if (root == null)
                return;

            StripSceneJunk(root);
            int n = 0;
            Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null || (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)))
                    continue;

                Material[] src = r.sharedMaterials;
                int slots = src != null && src.Length > 0 ? src.Length : 1;
                Material[] dst = new Material[slots];
                for (int m = 0; m < slots; m++)
                {
                    Material? old = src != null && m < src.Length ? src[m] : null;
                    string matName = old != null ? old.name : r.gameObject.name;
                    Material mat = VisualShader.Make(matName + "_ww", cull: 0f);
                    WriteTint(mat, PeekTint(old));

                    Texture? albedo = PeekAlbedo(old);
                    if (albedo != null)
                        WriteAlbedo(mat, albedo);
                    else
                        ClearAlbedoMaps(mat);

                    CopyMap(old, mat, "_BumpMap", "_BumpMap");
                    CopyMap(old, mat, "_BumpMap", "_NormalMap");
                    CopyMap(old, mat, "_MetallicGlossMap", "_MetallicGlossMap");
                    CopyMap(old, mat, "_OcclusionMap", "_OcclusionMap");
                    CopyGloss(old, mat);
                    ApplyWarewindNodeMaps(mat, matName, old);
                    KillEmission(mat);
                    dst[m] = mat;
                    n++;
                }

                r.sharedMaterials = dst;
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                r.receiveShadows = true;
            }

            WarewindPlugin.ModLog?.LogInfo(
                $"VisualMaterials FBX-look '{root.name}' slots={n} cull=Off shader={VisualShader.Lit.name}");
        }

        internal static void ApplySolidFbxColors(GameObject root) => ApplyFbxLook(root);

        private static void ClearAlbedoMaps(Material mat)
        {
            if (mat == null)
                return;
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", null);
            if (mat.HasProperty("_BaseColorMap"))
                mat.SetTexture("_BaseColorMap", null);
        }


        internal static void MatchHostDrawState(GameObject vis, GameObject host)
        {
            if (vis == null || host == null)
                return;

            int layer = host.layer;
            uint mask = 1u;
            Renderer? donor = null;
            Renderer[] hostRs = host.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < hostRs.Length; i++)
            {
                Renderer r = hostRs[i];
                if (r == null)
                    continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    continue;
                if (r.transform.name == "WarewindVisual")
                    continue;
                Transform t = r.transform;
                bool underVis = false;
                while (t != null)
                {
                    if (t.name == "WarewindVisual")
                    {
                        underVis = true;
                        break;
                    }
                    t = t.parent;
                }
                if (underVis)
                    continue;
                donor = r;
                layer = r.gameObject.layer;
                mask = r.renderingLayerMask;
                break;
            }

            Transform[] all = vis.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null)
                    all[i].gameObject.layer = layer;
            }

            Renderer[] visRs = vis.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < visRs.Length; i++)
            {
                Renderer r = visRs[i];
                if (r == null)
                    continue;
                r.renderingLayerMask = mask;
                if (donor != null)
                {
                    r.lightProbeUsage = donor.lightProbeUsage;
                    r.reflectionProbeUsage = donor.reflectionProbeUsage;
                }
            }
        }


        private static void CopyGloss(Material? src, Material dst)
        {
            if (src == null || dst == null)
                return;
            if (src.HasProperty("_Metallic") && dst.HasProperty("_Metallic"))
                dst.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            if (src.HasProperty("_Glossiness") && dst.HasProperty("_Smoothness"))
                dst.SetFloat("_Smoothness", src.GetFloat("_Glossiness"));
            else if (src.HasProperty("_Smoothness") && dst.HasProperty("_Smoothness"))
                dst.SetFloat("_Smoothness", src.GetFloat("_Smoothness"));
            if (src.HasProperty("_Glossiness") && dst.HasProperty("_Glossiness"))
                dst.SetFloat("_Glossiness", src.GetFloat("_Glossiness"));
        }

        private static void CopyMap(Material? src, Material dst, string srcProp, string dstProp)
        {
            if (src == null || dst == null || !src.HasProperty(srcProp) || !dst.HasProperty(dstProp))
                return;
            Texture t = src.GetTexture(srcProp);
            if (t != null)
                dst.SetTexture(dstProp, t);
        }

        /// <summary>
        /// Blender nodes → UV bake: Roughness (Noise+ColorRamp), Normal (Bump on carboning).
        /// Prefers maps already on baked mat; else Textures/Warewind/*.png next to DLL.
        /// </summary>
        private static void ApplyWarewindNodeMaps(Material mat, string matName, Material? baked)
        {
            if (mat == null)
                return;

            float metallic = 0f;
            if (mat.HasProperty("_Metallic"))
                metallic = mat.GetFloat("_Metallic");

            Texture? bump = baked != null && baked.HasProperty("_BumpMap") ? baked.GetTexture("_BumpMap") : null;
            if (bump == null)
                bump = WarewindMaps.Normal(matName);
            if (bump != null)
            {
                if (mat.HasProperty("_BumpMap"))
                    mat.SetTexture("_BumpMap", bump);
                if (mat.HasProperty("_NormalMap"))
                    mat.SetTexture("_NormalMap", bump);
                if (mat.HasProperty("_BumpScale"))
                    mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            Texture? metGloss = baked != null && baked.HasProperty("_MetallicGlossMap")
                ? baked.GetTexture("_MetallicGlossMap")
                : null;
            if (metGloss == null)
                metGloss = WarewindMaps.MetallicGloss(matName, metallic);
            if (metGloss != null)
            {
                if (mat.HasProperty("_MetallicGlossMap"))
                    mat.SetTexture("_MetallicGlossMap", metGloss);
                if (mat.HasProperty("_MaskMap"))
                    mat.SetTexture("_MaskMap", metGloss);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.EnableKeyword("_METALLICGLOSSMAP");
                if (mat.HasProperty("_SmoothnessTextureChannel"))
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                // Map carries metallic(R)+smoothness(A); slider = multiplier.
                if (mat.HasProperty("_Metallic"))
                    mat.SetFloat("_Metallic", 1f);
                if (mat.HasProperty("_Smoothness"))
                    mat.SetFloat("_Smoothness", 1f);
                if (mat.HasProperty("_GlossMapScale"))
                    mat.SetFloat("_GlossMapScale", 1f);
                if (mat.HasProperty("_Glossiness"))
                    mat.SetFloat("_Glossiness", 1f);
            }
        }

        private static Color PeekTint(Material? mat)
        {
            if (mat == null)
                return Color.white;
            if (mat.HasProperty("_BaseColor"))
                return mat.GetColor("_BaseColor");
            if (mat.HasProperty("_Color"))
                return mat.GetColor("_Color");
            return Color.white;
        }

        private static void WriteTint(Material mat, Color tint)
        {
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tint);
        }

        private static void KillEmission(Material mat)
        {
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);
            if (mat.HasProperty("_EmissiveColor"))
                mat.SetColor("_EmissiveColor", Color.black);
            if (mat.HasProperty("_EmissionMap"))
                mat.SetTexture("_EmissionMap", null);
            if (mat.HasProperty("_EmissiveColorMap"))
                mat.SetTexture("_EmissiveColorMap", null);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        private static Texture? PeekAlbedo(Material? mat)
        {
            if (mat == null)
                return null;
            if (mat.HasProperty("_BaseMap"))
            {
                Texture t = mat.GetTexture("_BaseMap");
                if (t != null)
                    return t;
            }
            if (mat.HasProperty("_MainTex"))
            {
                Texture t = mat.GetTexture("_MainTex");
                if (t != null)
                    return t;
            }
            return null;
        }

        private static void WriteAlbedo(Material mat, Texture tex)
        {
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                VisualShader.ResetSt(mat, "_BaseMap");
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
                VisualShader.ResetSt(mat, "_MainTex");
            }
            if (mat.HasProperty("_BaseColorMap"))
            {
                mat.SetTexture("_BaseColorMap", tex);
                VisualShader.ResetSt(mat, "_BaseColorMap");
            }
        }

    }
}
