using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Warewind.Runtime
{
    /// <summary>
    /// Warewind Blender procedural bake: Textures/Warewind/{Mat}_Roughness.png + _Normal.png.
    /// Noise→ColorRamp→Roughness; carboning also ColorRamp→Bump→Normal.
    /// </summary>
    internal static class WarewindMaps
    {
        private static readonly Dictionary<string, Texture2D> Cache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static string? _dir;
        private static bool _dirTried;

        internal static Texture2D? Roughness(string blenderMatName) =>
            Load(StripSuffix(blenderMatName) + "_Roughness");

        internal static Texture2D? Normal(string blenderMatName) =>
            Load(StripSuffix(blenderMatName) + "_Normal");

        internal static Texture2D? SmoothnessFromRoughness(string blenderMatName)
        {
            string key = StripSuffix(blenderMatName) + "_Smoothness";
            if (Cache.TryGetValue(key, out Texture2D hit))
                return hit;

            Texture2D? rough = Roughness(blenderMatName);
            if (rough == null)
                return null;

            try
            {
                Color32[] px = rough.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    byte inv = (byte)(255 - px[i].r);
                    px[i] = new Color32(inv, inv, inv, 255);
                }

                var smooth = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, true, linear: true);
                smooth.name = key;
                smooth.wrapMode = TextureWrapMode.Repeat;
                smooth.filterMode = FilterMode.Bilinear;
                smooth.anisoLevel = 4;
                smooth.SetPixels32(px);
                smooth.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                Cache[key] = smooth;
                return smooth;
            }
            catch (Exception ex)
            {
                WarewindPlugin.ModLog?.LogWarning($"WarewindMaps smoothness '{key}': {ex.Message}");
                return null;
            }
        }

        /// <summary>URP/Standard metallic-gloss: R=metallic, A=smoothness (1−roughness).</summary>
        internal static Texture2D? MetallicGloss(string blenderMatName, float metallic)
        {
            string key = StripSuffix(blenderMatName) + "_MetGloss_" + metallic.ToString("0.###");
            if (Cache.TryGetValue(key, out Texture2D hit))
                return hit;

            Texture2D? rough = Roughness(blenderMatName);
            if (rough == null)
                return null;

            try
            {
                Color32[] px = rough.GetPixels32();
                byte met = (byte)Mathf.Clamp(Mathf.RoundToInt(metallic * 255f), 0, 255);
                for (int i = 0; i < px.Length; i++)
                {
                    byte smooth = (byte)(255 - px[i].r);
                    px[i] = new Color32(met, met, met, smooth);
                }

                var tex = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, true, linear: true);
                tex.name = key;
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Bilinear;
                tex.anisoLevel = 4;
                tex.SetPixels32(px);
                tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                Cache[key] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                WarewindPlugin.ModLog?.LogWarning($"WarewindMaps metGloss '{key}': {ex.Message}");
                return null;
            }
        }

        private static string StripSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "mat";
            string n = name;
            int i = n.LastIndexOf("_ww", StringComparison.OrdinalIgnoreCase);
            if (i > 0)
                n = n.Substring(0, i);
            i = n.LastIndexOf("_runtime", StringComparison.OrdinalIgnoreCase);
            if (i > 0)
                n = n.Substring(0, i);
            i = n.LastIndexOf("_Mat", StringComparison.OrdinalIgnoreCase);
            if (i > 0)
                n = n.Substring(0, i);
            return n;
        }

        private static Texture2D? Load(string stem)
        {
            if (string.IsNullOrEmpty(stem))
                return null;
            if (Cache.TryGetValue(stem, out Texture2D hit))
                return hit;

            EnsureDir();
            if (string.IsNullOrEmpty(_dir))
                return null;

            string path = Path.Combine(_dir, stem + ".png");
            if (!File.Exists(path))
            {
                Cache[stem] = null!;
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                bool linear = stem.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              stem.IndexOf("Roughness", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              stem.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) >= 0;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear);
                if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
                {
                    Cache[stem] = null!;
                    return null;
                }

                tex.name = stem;
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Bilinear;
                tex.anisoLevel = 4;

                if (stem.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0)
                    PackNormalAg(tex);

                Cache[stem] = tex;
                WarewindPlugin.ModLog?.LogInfo($"WarewindMaps loaded '{stem}' {tex.width}x{tex.height}");
                return tex;
            }
            catch (Exception ex)
            {
                WarewindPlugin.ModLog?.LogWarning($"WarewindMaps '{stem}': {ex.Message}");
                Cache[stem] = null!;
                return null;
            }
        }

        /// <summary>Blender RGB tangent normal → Unity AG for UnpackNormalmapRGorAG.</summary>
        private static void PackNormalAg(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                byte x = px[i].r;
                byte y = px[i].g;
                px[i] = new Color32(255, y, 255, x);
            }
            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
        }

        private static void EnsureDir()
        {
            if (_dirTried)
                return;
            _dirTried = true;
            string? plugin = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(plugin))
                return;
            string local = Path.Combine(plugin, "Textures", "Warewind");
            if (Directory.Exists(local))
                _dir = local;
        }
    }
}
