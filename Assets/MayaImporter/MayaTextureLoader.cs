// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.IO;
using MayaImporter.Components;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MayaImporter.Core
{
    public enum MayaTextureUsage
    {
        Unknown = 0,
        BaseColor,
        Normal,
        Metallic,
        Roughness,
        Emission,
        Opacity,
    }

    public readonly struct MayaTextureLoadInfo
    {
        public readonly string resolvedAbsolutePath;
        public readonly string resolvedAssetsPath;
        public readonly bool isUdim;
        public readonly int udimTileCount;

        public MayaTextureLoadInfo(string abs, string assets, bool udim, int tileCount)
        {
            resolvedAbsolutePath = abs;
            resolvedAssetsPath = assets;
            isUdim = udim;
            udimTileCount = tileCount;
        }
    }

    public static class MayaTextureLoader
    {
        private static readonly Dictionary<string, Texture2D> s_cache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Phase B:
        /// - UDIM^C͌ێiB-5j
        /// - EditorTextureImporterݒ usage ɉĕ␳iB-3j
        /// - 摜ǂ߂Ȃ/Ȃꍇ null Ԃ import ~߂Ȃi100%ێvzj
        /// </summary>
        public static Texture LoadTexture_BestEffort(
            MayaSceneData scene,
            MayaTextureMetadata meta,
            MayaTextureUsage usage,
            MayaImportLog log,
            out MayaTextureLoadInfo info)
        {
            log ??= new MayaImportLog();
            info = new MayaTextureLoadInfo("", "", false, 0);

            if (meta == null) return null;

            var path = meta.fileTextureName;
            if (string.IsNullOrEmpty(path)) return null;

            // UDIM detect
            var isUdim = IsUdimPattern(path);
            if (isUdim)
            {
                var udimPaths = ResolveUdimTilePaths(scene, path, log);
                AttachUdimSetComponent(meta.gameObject, path, udimPaths, log);

                // \p 1001 DŃ[hiUnity̕WShaderłUDIMԂ𐳂\łȂ߁j
                //  gf[^[h  MayaUdimSetMetadata ۏ؂B
                if (udimPaths.Count > 0)
                {
                    var first = PickUdimPreviewTile(udimPaths);
                    var tex = LoadTexture2D_BestEffort(scene, first, usage, log, out var abs, out var assets);
                    info = new MayaTextureLoadInfo(abs, assets, true, udimPaths.Count);
                    return tex;
                }

                // no tiles -> warn and return null
                log.Warn($"[TextureLoader] UDIM tiles not found for pattern: '{path}' node='{meta.gameObject.name}'");
                info = new MayaTextureLoadInfo("", "", true, 0);
                return null;
            }

            var tex2 = LoadTexture2D_BestEffort(scene, path, usage, log, out var absPath, out var assetsPath);
            info = new MayaTextureLoadInfo(absPath, assetsPath, false, 0);
            return tex2;
        }

        // ----------------------------
        // Single texture
        // ----------------------------
        private static Texture2D LoadTexture2D_BestEffort(
            MayaSceneData scene,
            string mayaPath,
            MayaTextureUsage usage,
            MayaImportLog log,
            out string resolvedAbs,
            out string resolvedAssets)
        {
            resolvedAbs = "";
            resolvedAssets = "";

            if (string.IsNullOrEmpty(mayaPath)) return null;

            var (absPath, assetsPath) = ResolvePath(scene, mayaPath);
            resolvedAbs = absPath;
            resolvedAssets = assetsPath;

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(assetsPath) && assetsPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                // (B-3) usageɉ TextureImporter ݒ␳iNormal/Roughness/Metallicj
                MayaTextureImportSettings.EnsureImporterSettings_BestEffort(assetsPath, usage, log);

                var projTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetsPath);
                if (projTex != null) return projTex;
            }
#endif

            if (string.IsNullOrEmpty(absPath) || !File.Exists(absPath))
            {
                log.Warn($"[TextureLoader] Texture file not found: '{mayaPath}' (resolved='{absPath}')");
                return null;
            }

            if (s_cache.TryGetValue(absPath, out var cached) && cached != null)
                return cached;

            try
            {
                var bytes = File.ReadAllBytes(absPath);
                if (bytes == null || bytes.Length == 0)
                {
                    log.Warn($"[TextureLoader] Empty texture bytes: {absPath}");
                    return null;
                }

                // Normal/Roughness/Metallic  linear Ɋ񂹂
                var linear = (usage == MayaTextureUsage.Normal || usage == MayaTextureUsage.Roughness || usage == MayaTextureUsage.Metallic);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true, linear: linear);
                tex.name = SanitizeName(Path.GetFileNameWithoutExtension(absPath));

                if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    log.Warn($"[TextureLoader] LoadImage failed (unsupported format?) : {absPath}");
                    return null;
                }

                tex.wrapModeU = TextureWrapMode.Repeat;
                tex.wrapModeV = TextureWrapMode.Repeat;

                s_cache[absPath] = tex;
                return tex;
            }
            catch (Exception e)
            {
                log.Warn($"[TextureLoader] Exception reading '{absPath}': {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        // ----------------------------
        // UDIM (B-5)
        // ----------------------------
        private static bool IsUdimPattern(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.IndexOf("<UDIM>", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (path.IndexOf("%04d", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (path.IndexOf("####", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string ToUdimGlobBase(string path)
        {
            // Convert pattern to a wildcard-friendly base (directory + prefix/suffix)
            // We'll scan directory for 4-digit UDIM numbers in filename.
            return path
                .Replace("<UDIM>", "####")
                .Replace("%04d", "####");
        }

        private static List<string> ResolveUdimTilePaths(MayaSceneData scene, string udimPatternPath, MayaImportLog log)
        {
            log ??= new MayaImportLog();
            var results = new List<string>(32);

            // Resolve pattern's directory as best-effort
            var (absMaybe, assetsMaybe) = ResolvePath(scene, ToUdimGlobBase(udimPatternPath));

            // If inside Assets, search project for matching files
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(assetsMaybe) && assetsMaybe.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                // best-effort: search in same folder for files containing 1001..1999
                var folder = Path.GetDirectoryName(assetsMaybe)?.Replace('\\', '/') ?? "Assets";
                var searchExt = Path.GetExtension(assetsMaybe);
                var guids = AssetDatabase.FindAssets("", new[] { folder });

                for (int i = 0; i < guids.Length; i++)
                {
                    var p = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                    if (string.IsNullOrEmpty(p)) continue;
                    if (!string.IsNullOrEmpty(searchExt) && !p.EndsWith(searchExt, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // filename contains 4-digit UDIM?
                    var fn = Path.GetFileNameWithoutExtension(p);
                    if (TryExtractUdim(fn, out var udim))
                    {
                        // rough filter: UDIM range
                        if (udim >= 1001 && udim <= 1999)
                            results.Add(p);
                    }
                }

                results.Sort(UdimPathComparer);
                return results;
            }
#endif

            // If absolute directory exists, scan it
            try
            {
                var abs = absMaybe;
                if (string.IsNullOrEmpty(abs)) return results;

                var dir = Path.GetDirectoryName(abs);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    return results;

                var ext = Path.GetExtension(abs);
                var files = Directory.GetFiles(dir, "*" + ext, SearchOption.TopDirectoryOnly);

                for (int i = 0; i < files.Length; i++)
                {
                    var f = files[i];
                    var fn = Path.GetFileNameWithoutExtension(f);
                    if (TryExtractUdim(fn, out var udim))
                    {
                        if (udim >= 1001 && udim <= 1999)
                            results.Add(f);
                    }
                }

                results.Sort(UdimPathComparer);
                return results;
            }
            catch (Exception e)
            {
                log.Warn("[TextureLoader] UDIM scan exception: " + e.Message);
                return results;
            }
        }

        private static int UdimPathComparer(string a, string b)
        {
            var ua = ExtractUdimFromPath(a);
            var ub = ExtractUdimFromPath(b);
            return ua.CompareTo(ub);
        }

        private static int ExtractUdimFromPath(string p)
        {
            var fn = Path.GetFileNameWithoutExtension(p);
            if (TryExtractUdim(fn, out var udim)) return udim;
            return int.MaxValue;
        }

        private static string PickUdimPreviewTile(List<string> udimPaths)
        {
            // prefer 1001
            for (int i = 0; i < udimPaths.Count; i++)
            {
                if (ExtractUdimFromPath(udimPaths[i]) == 1001)
                    return udimPaths[i];
            }
            return udimPaths[0];
        }

        private static bool TryExtractUdim(string filenameNoExt, out int udim)
        {
            // find any 4-digit run that looks like UDIM (1001-1999)
            udim = 0;
            if (string.IsNullOrEmpty(filenameNoExt)) return false;

            for (int i = 0; i <= filenameNoExt.Length - 4; i++)
            {
                if (!char.IsDigit(filenameNoExt[i])) continue;
                if (!char.IsDigit(filenameNoExt[i + 1])) continue;
                if (!char.IsDigit(filenameNoExt[i + 2])) continue;
                if (!char.IsDigit(filenameNoExt[i + 3])) continue;

                var s = filenameNoExt.Substring(i, 4);
                if (int.TryParse(s, out var n) && n >= 1001 && n <= 1999)
                {
                    udim = n;
                    return true;
                }
            }
            return false;
        }

        private static void AttachUdimSetComponent(GameObject go, string pattern, List<string> tiles, MayaImportLog log)
        {
            if (go == null) return;

            var comp = go.GetComponent<MayaUdimSetMetadata>();
            if (comp == null)
                comp = go.AddComponent<MayaUdimSetMetadata>();

            comp.udimPattern = pattern;
            comp.tilePaths = tiles.ToArray();
            comp.previewTileUdim = (tiles.Count > 0) ? ExtractUdimFromPath(PickUdimPreviewTile(tiles)) : 0;

            if (tiles.Count > 0)
                log?.Info($"[TextureLoader] UDIM detected node='{go.name}' tiles={tiles.Count} preview={comp.previewTileUdim}");
        }

        // ----------------------------
        // Path resolve
        // ----------------------------
        private static (string absPath, string assetsPath) ResolvePath(MayaSceneData scene, string mayaPath)
        {
            if (string.IsNullOrEmpty(mayaPath)) return ("", "");

            if (mayaPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
#if UNITY_EDITOR
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
                if (!string.IsNullOrEmpty(projectRoot))
                {
                    var abs = Path.GetFullPath(Path.Combine(projectRoot, mayaPath));
                    return (abs, mayaPath.Replace('\\', '/'));
                }
#endif
                return ("", mayaPath.Replace('\\', '/'));
            }

            try
            {
                if (Path.IsPathRooted(mayaPath))
                {
                    var abs = Path.GetFullPath(mayaPath);
                    return (abs, "");
                }
            }
            catch { }

            var baseDir = "";
            try
            {
                if (scene != null && !string.IsNullOrEmpty(scene.SourcePath))
                {
                    if (Path.IsPathRooted(scene.SourcePath))
                        baseDir = Path.GetDirectoryName(scene.SourcePath);
#if UNITY_EDITOR
                    else if (scene.SourcePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
                        if (!string.IsNullOrEmpty(projectRoot))
                        {
                            var absScene = Path.GetFullPath(Path.Combine(projectRoot, scene.SourcePath));
                            baseDir = Path.GetDirectoryName(absScene);
                        }
                    }
#endif
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(baseDir))
            {
                var abs = Path.GetFullPath(Path.Combine(baseDir, mayaPath));
                return (abs, "");
            }

            try
            {
                var abs2 = Path.GetFullPath(mayaPath);
                return (abs2, "");
            }
            catch
            {
                return ("", "");
            }
        }

        private static string SanitizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Texture";
            s = s.Replace(' ', '_').Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            return s;
        }
    }
}
