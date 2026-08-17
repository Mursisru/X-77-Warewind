using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Warewind.Blueprinter
{
    /// <summary>Loads WarewindVisual from X77Warewind.nobp (reuses an already-loaded bundle if present).</summary>
    internal static class NobpContent
    {
        private static AssetBundle? _bundle;
        private static GameObject? _warewindVisual;
        private static bool _tried;

        internal static GameObject? WarewindVisual => _warewindVisual;

        internal static void TryLoad()
        {
            if (_tried)
                return;
            _tried = true;

            try
            {
                _bundle = FindLoadedBundle() ?? LoadFromDiskOrEmbedded();
                if (_bundle == null)
                {
                    WarewindPlugin.ModLog?.LogWarning("X77Warewind.nobp not available — hangar/flight mesh stamp skipped.");
                    return;
                }

                _warewindVisual = _bundle.LoadAsset<GameObject>(WarewindConstants.MeshPrefabAsset);
                if (_warewindVisual == null)
                {
                    GameObject[] all = _bundle.LoadAllAssets<GameObject>();
                    if (all != null)
                    {
                        for (int i = 0; i < all.Length; i++)
                        {
                            GameObject go = all[i];
                            if (go == null)
                                continue;
                            if (go.name.IndexOf("Warewind", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _warewindVisual = go;
                                break;
                            }
                        }
                    }
                }

                if (_warewindVisual != null)
                    WarewindPlugin.ModLog?.LogInfo($"Warewind visual ready: '{_warewindVisual.name}'");
                else
                    WarewindPlugin.ModLog?.LogWarning("nobp loaded but no WarewindVisual found — rebake X77Warewind.nobp.");
            }
            catch (Exception ex)
            {
                WarewindPlugin.ModLog?.LogError($"NobpContent: {ex}");
            }
        }

        private static AssetBundle? FindLoadedBundle()
        {
            foreach (AssetBundle b in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (b == null)
                    continue;
                try
                {
                    if (b.Contains(WarewindConstants.MeshPrefabAsset))
                    {
                        WarewindPlugin.ModLog?.LogInfo($"Reusing loaded AssetBundle '{b.name}'");
                        return b;
                    }
                }
                catch
                {
                    // ignore
                }
            }
            return null;
        }

        private static AssetBundle? LoadFromDiskOrEmbedded()
        {
            string? path = FindNobpPath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                AssetBundle? fromFile = AssetBundle.LoadFromFile(path);
                if (fromFile != null)
                {
                    WarewindPlugin.ModLog?.LogInfo($"Loaded .nobp from file: {path}");
                    return fromFile;
                }
                WarewindPlugin.ModLog?.LogWarning($"LoadFromFile returned null (already loaded?): {path}");
            }

            return LoadEmbeddedNobp();
        }

        private static string? FindNobpPath()
        {
            string? pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(pluginDir))
                return null;

            string? best = null;
            long bestSize = 0;

            void Consider(string candidate)
            {
                if (!File.Exists(candidate))
                    return;
                long len = new FileInfo(candidate).Length;
                if (len < 4096)
                    return;
                if (len > bestSize)
                {
                    bestSize = len;
                    best = candidate;
                }
            }

            Consider(Path.Combine(pluginDir, WarewindConstants.NobpFileName));
            Consider(Path.Combine(pluginDir, "MissilePack.nobp"));
            Consider(Path.Combine(pluginDir, "missilepack.nobp"));

            foreach (string f in Directory.GetFiles(pluginDir, "*.nobp"))
            {
                string n = Path.GetFileName(f);
                if (n.IndexOf("X77", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Warewind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("MissilePack", StringComparison.OrdinalIgnoreCase) >= 0)
                    Consider(f);
            }

            return best;
        }

        private static AssetBundle? LoadEmbeddedNobp()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (!name.EndsWith(".nobp", StringComparison.OrdinalIgnoreCase))
                    continue;
                using Stream? stream = asm.GetManifestResourceStream(name);
                if (stream == null)
                    continue;
                using MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                AssetBundle? b = AssetBundle.LoadFromMemory(ms.ToArray());
                if (b != null)
                {
                    WarewindPlugin.ModLog?.LogInfo($"Loaded embedded .nobp: {name}");
                    return b;
                }
            }
            return null;
        }
    }
}
