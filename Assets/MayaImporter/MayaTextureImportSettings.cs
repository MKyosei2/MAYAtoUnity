#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase B-3:
    /// - Project“à(Assets/...) ‚Ì TextureImporter ‚ğ usage ‚É‰‚¶‚Ä©“®•â³
    /// - NormalMap ‚ğ gŠmÀ‚ÉNormal‚Æ‚µ‚Äˆµ‚¤h
    /// - Roughness/Metallic ‚Í linear ‚ÉŠñ‚¹‚éisRGB offj
    /// </summary>
    public static class MayaTextureImportSettings
    {
        public static void EnsureImporterSettings_BestEffort(string assetsPath, MayaTextureUsage usage, MayaImportLog log)
        {
            if (string.IsNullOrEmpty(assetsPath)) return;
            if (!assetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return;
            if (usage == MayaTextureUsage.Unknown || usage == MayaTextureUsage.BaseColor || usage == MayaTextureUsage.Emission)
                return;

            try
            {
                var importer = AssetImporter.GetAtPath(assetsPath) as TextureImporter;
                if (importer == null) return;

                bool changed = false;

                // sRGB
                if (usage == MayaTextureUsage.Normal || usage == MayaTextureUsage.Roughness || usage == MayaTextureUsage.Metallic || usage == MayaTextureUsage.Opacity)
                {
                    if (importer.sRGBTexture)
                    {
                        importer.sRGBTexture = false;
                        changed = true;
                    }
                }

                // NormalMap
                if (usage == MayaTextureUsage.Normal)
                {
                    if (importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        changed = true;
                    }
                }

                // Wrap / mipmap best-effort
                if (importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    changed = true;
                }

                if (!importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = true;
                    changed = true;
                }

                if (changed)
                {
                    // reimport
                    AssetDatabase.ImportAsset(assetsPath, ImportAssetOptions.ForceUpdate);
                    log?.Info($"[TextureImportSettings] Applied importer settings: {usage} -> {assetsPath}");
                }
            }
            catch (Exception e)
            {
                log?.Warn($"[TextureImportSettings] Exception for '{assetsPath}': {e.GetType().Name}: {e.Message}");
            }
        }
    }
}
#endif
