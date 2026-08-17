using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Warewind.UnityBake
{
    /// <summary>Builds X77Warewind.nobp (WarewindVisual + patch_manifest).</summary>
    public static class NobpBundleBuilder
    {
        private const string PrefabName = "WarewindVisual";
        private const string WarewindPrefabName = "WarewindVisual";
        private const string WarewindFbxName = "X-75-Warewind.fbx";
        private const string OutputName = "X77Warewind.nobp";

        [MenuItem("Warewind/Build Nobp Bundle")]
        public static void Build()
        {
            string assetsRoot = "Assets/MissilePack";
            string buildDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
            Directory.CreateDirectory(buildDir);

            EnsureWarewindPrefab(assetsRoot);
            EnsureManifest(assetsRoot);

            string prefabPath = $"{assetsRoot}/{PrefabName}.prefab";
            string manifestPath = $"{assetsRoot}/patch_manifest.txt";

            List<string> assetNames = new List<string> { prefabPath, manifestPath };

            string matFolder = $"{assetsRoot}/Materials/Warewind";
            if (AssetDatabase.IsValidFolder(matFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { matFolder }))
                    assetNames.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            string texFolder = $"{assetsRoot}/Textures/Warewind";
            if (AssetDatabase.IsValidFolder(texFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }))
                    assetNames.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            string wareFbx = FindNamedFbx(assetsRoot, WarewindFbxName);
            if (!string.IsNullOrEmpty(wareFbx) && !assetNames.Contains(wareFbx))
                assetNames.Add(wareFbx);

            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = OutputName,
                assetNames = assetNames.ToArray()
            };

            BuildPipeline.BuildAssetBundles(
                buildDir,
                new[] { build },
                BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);

            string produced = Path.Combine(buildDir, OutputName);
            string alt = Path.Combine(buildDir, OutputName.ToLowerInvariant());
            if (!File.Exists(produced) && File.Exists(alt))
                File.Copy(alt, produced, true);

            string pluginRes = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "X77Warewind", "Resources"));
            Directory.CreateDirectory(pluginRes);
            if (File.Exists(produced))
            {
                File.Copy(produced, Path.Combine(pluginRes, OutputName), true);
                string binRel = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "X77Warewind", "bin", "Release"));
                Directory.CreateDirectory(binRel);
                File.Copy(produced, Path.Combine(binRel, OutputName), true);
            }

            string deploy = @"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\X-77-Warewind";
            Directory.CreateDirectory(deploy);
            if (File.Exists(produced))
            {
                File.Copy(produced, Path.Combine(deploy, OutputName), true);
                File.Copy(produced, Path.Combine(deploy, OutputName.ToLowerInvariant()), true);
            }

            string wwTex = Path.Combine(Application.dataPath, "MissilePack", "Textures", "Warewind");
            if (Directory.Exists(wwTex))
            {
                string wwDeploy = Path.Combine(deploy, "Textures", "Warewind");
                Directory.CreateDirectory(wwDeploy);
                foreach (string file in Directory.GetFiles(wwTex, "*.png"))
                    File.Copy(file, Path.Combine(wwDeploy, Path.GetFileName(file)), true);
            }

            Debug.Log($"Warewind: built {produced}");
            AssetDatabase.Refresh();
        }


        private static void EnsureManifest(string assetsRoot)
        {
            string json =
@"{
  ""modName"": ""X77Warewind"",
  ""schemaVersion"": 3,
  ""modVersion"": ""0.0.0"",
  ""Patches"": [],
  ""Ops"": [],
  ""Addressables"": []
}";
            string txtPath = Path.Combine(Application.dataPath, "MissilePack", "patch_manifest.txt");
            File.WriteAllText(txtPath, json);
            AssetDatabase.ImportAsset($"{assetsRoot}/patch_manifest.txt");
        }

        private static void EnsureWarewindPrefab(string assetsRoot)
        {
            string fbxPath = FindNamedFbx(assetsRoot, WarewindFbxName);
            if (string.IsNullOrEmpty(fbxPath))
            {
                Debug.LogWarning("MissilePack: X-75-Warewind.fbx not found — WarewindVisual skipped.");
                return;
            }

            ConfigureWarewindImporter(fbxPath);
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null)
            {
                Debug.LogError("MissilePack: failed to load X-75-Warewind.fbx");
                return;
            }

            GameObject root = UnityEngine.Object.Instantiate(fbx);
            root.name = WarewindPrefabName;

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

            Shader lit = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            string matFolder = $"{assetsRoot}/Materials/Warewind";
            if (!AssetDatabase.IsValidFolder($"{assetsRoot}/Materials"))
                AssetDatabase.CreateFolder(assetsRoot, "Materials");
            if (!AssetDatabase.IsValidFolder(matFolder))
                AssetDatabase.CreateFolder($"{assetsRoot}/Materials", "Warewind");

            Dictionary<string, Material> bakedByBlender = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Material> fbxMats = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Texture> fbxTex = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (sub is Material mat)
                    fbxMats[mat.name] = mat;
                else if (sub is Texture tex)
                    fbxTex[tex.name] = tex;
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                Material[] src = r.sharedMaterials;
                Material[] dst = new Material[Mathf.Max(1, src != null ? src.Length : 1)];
                string meshName = r.gameObject.name;
                for (int i = 0; i < dst.Length; i++)
                {
                    Material imported = src != null && i < src.Length ? src[i] : null;
                    if (imported == null)
                        fbxMats.TryGetValue(meshName, out imported);

                    string blenderName = imported != null && !string.IsNullOrEmpty(imported.name)
                        ? imported.name
                        : meshName + "_" + i;
                    if (bakedByBlender.TryGetValue(blenderName, out Material shared))
                    {
                        dst[i] = shared;
                        continue;
                    }

                    string matAssetPath = $"{matFolder}/{Sanitize(blenderName)}.mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
                    if (mat == null)
                    {
                        mat = imported != null ? new Material(imported) : new Material(lit);
                        mat.name = blenderName;
                        AssetDatabase.CreateAsset(mat, matAssetPath);
                    }
                    else if (imported != null)
                        mat.CopyPropertiesFromMaterial(imported);

                    mat.name = blenderName;
                    if (mat.shader == null || mat.shader.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                        mat.shader = lit;

                    Texture importAlbedo = imported != null ? PeekAlbedo(imported) : PeekAlbedo(mat);
                    if (importAlbedo != null)
                        WriteAlbedo(mat, importAlbedo);
                    else
                    {
                        foreach (KeyValuePair<string, Texture> kv in fbxTex)
                        {
                            if (kv.Key.IndexOf(blenderName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                kv.Key.IndexOf(meshName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                WriteAlbedo(mat, kv.Value);
                                break;
                            }
                        }
                    }

                    RestoreBlenderMetallic(mat, blenderName);
                    ApplyWarewindDiskMaps(mat, blenderName, assetsRoot);

                    if (mat.HasProperty("_EmissionColor"))
                        mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                    EditorUtility.SetDirty(mat);
                    bakedByBlender[blenderName] = mat;
                    dst[i] = mat;
                }
                r.sharedMaterials = dst;
            }

            AssetDatabase.SaveAssets();
            string prefabPath = $"{assetsRoot}/{WarewindPrefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log($"MissilePack: WarewindVisual from '{fbxPath}'");
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "mesh";
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static string FindNamedFbx(string assetsRoot, string fileName)
        {
            string preferred = $"{assetsRoot}/{fileName}";
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), preferred.Replace('/', Path.DirectorySeparatorChar))))
                return preferred;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets(stem + " t:Model");
            if (guids == null || guids.Length == 0)
                return null;
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        private static string FindAsset(string preferred)
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), preferred.Replace('/', Path.DirectorySeparatorChar))))
                return preferred;
            string[] guids = AssetDatabase.FindAssets("RealtorpedoTransformMK54L t:Model");
            if (guids.Length == 0)
                return null;
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        // Unity Phong import drops Principled metallic. Values from X-75-Warewind.blend.
        private static void RestoreBlenderMetallic(Material mat, string blenderName)
        {
            if (mat == null || !mat.HasProperty("_Metallic"))
                return;
            bool glossy = !string.IsNullOrEmpty(blenderName) &&
                          blenderName.IndexOf("GlossyBlackMetal", StringComparison.OrdinalIgnoreCase) >= 0;
            mat.SetFloat("_Metallic", glossy ? 1f : 0f);
        }

        /// <summary>
        /// Noise→ColorRamp roughness (+ Bump normal on carboning) baked to Textures/Warewind/.
        /// </summary>
        private static void ApplyWarewindDiskMaps(Material mat, string blenderName, string assetsRoot)
        {
            if (mat == null || string.IsNullOrEmpty(blenderName))
                return;

            string texRoot = $"{assetsRoot}/Textures/Warewind";
            string stem = blenderName;
            float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texRoot}/{stem}_Normal.png");
            if (normal != null)
            {
                ConfigureTextureImport($"{texRoot}/{stem}_Normal.png", asNormal: true);
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texRoot}/{stem}_Normal.png");
                if (mat.HasProperty("_BumpMap"))
                    mat.SetTexture("_BumpMap", normal);
                if (mat.HasProperty("_BumpScale"))
                    mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texRoot}/{stem}_Roughness.png");
            if (roughness != null)
            {
                ConfigureTextureImport($"{texRoot}/{stem}_Roughness.png", asNormal: false);
                roughness = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texRoot}/{stem}_Roughness.png");
                Texture2D metGloss = BuildMetallicGlossAsset(texRoot, stem, roughness, metallic);
                if (metGloss != null)
                {
                    if (mat.HasProperty("_MetallicGlossMap"))
                        mat.SetTexture("_MetallicGlossMap", metGloss);
                    if (mat.HasProperty("_GlossMapScale"))
                        mat.SetFloat("_GlossMapScale", 1f);
                    if (mat.HasProperty("_Glossiness"))
                        mat.SetFloat("_Glossiness", 1f);
                    if (mat.HasProperty("_Smoothness"))
                        mat.SetFloat("_Smoothness", 1f);
                    if (mat.HasProperty("_Metallic"))
                        mat.SetFloat("_Metallic", 1f);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                }
            }
        }

        private static void ConfigureTextureImport(string assetPath, bool asNormal)
        {
            TextureImporter imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null)
                return;
            bool dirty = false;
            if (asNormal && imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                dirty = true;
            }
            if (!asNormal && imp.sRGBTexture)
            {
                imp.sRGBTexture = false;
                dirty = true;
            }
            if (imp.mipmapEnabled != true)
            {
                imp.mipmapEnabled = true;
                dirty = true;
            }
            if (dirty)
                imp.SaveAndReimport();
        }

        private static Texture2D BuildMetallicGlossAsset(string texRoot, string stem, Texture2D roughness, float metallic)
        {
            string outPath = $"{texRoot}/{stem}_MetallicGloss.png";
            string absOut = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outPath.Replace('/', Path.DirectorySeparatorChar)));
            string absRough = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDatabase.GetAssetPath(roughness).Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(absRough))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);

            try
            {
                byte[] bytes = File.ReadAllBytes(absRough);
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!ImageConversion.LoadImage(src, bytes, false))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);

                Color32[] px = src.GetPixels32();
                byte met = (byte)Mathf.Clamp(Mathf.RoundToInt(metallic * 255f), 0, 255);
                for (int i = 0; i < px.Length; i++)
                {
                    byte smooth = (byte)(255 - px[i].r);
                    px[i] = new Color32(met, met, met, smooth);
                }

                var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
                dst.SetPixels32(px);
                byte[] png = ImageConversion.EncodeToPNG(dst);
                Directory.CreateDirectory(Path.GetDirectoryName(absOut));
                File.WriteAllBytes(absOut, png);
                UnityEngine.Object.DestroyImmediate(src);
                UnityEngine.Object.DestroyImmediate(dst);
                AssetDatabase.ImportAsset(outPath);
                ConfigureTextureImport(outPath, asNormal: false);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("MissilePack: MetallicGloss bake failed for " + stem + ": " + ex.Message);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            }
        }

        private static void ConfigureWarewindImporter(string fbxPath)
        {
            ModelImporter imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null)
            {
                AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
                return;
            }

            imp.weldVertices = false;
            imp.meshOptimizationFlags = (MeshOptimizationFlags)0;
            imp.importNormals = ModelImporterNormals.Import;
            imp.preserveHierarchy = true;
            imp.SaveAndReimport();
        }

        private static bool IsKozuchName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return name.IndexOf("Kozuch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Кожух", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture PeekAlbedo(Material mat)
        {
            if (mat == null)
                return null;
            if (mat.HasProperty("_MainTex"))
            {
                Texture t = mat.GetTexture("_MainTex");
                if (t != null)
                    return t;
            }
            if (mat.HasProperty("_BaseMap"))
            {
                Texture t = mat.GetTexture("_BaseMap");
                if (t != null)
                    return t;
            }
            return null;
        }

        private static void WriteAlbedo(Material mat, Texture tex)
        {
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
        }
    }
}
