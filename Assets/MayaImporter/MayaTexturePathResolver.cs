// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Maya-like texture path resolver (Maya/API not required).
    /// Handles:
    /// - Absolute / relative paths
    /// - Common Maya project folders (sourceimages, images, textures, etc.)
    /// - $VARS and ${VARS}
    /// - UDIM tokens (<UDIM>, <udim>, %04d patterns best-effort)
    /// </summary>
    public static class MayaTexturePathResolver
    {
        // Common Maya project texture directories (relative to scene folder)
        private static readonly string[] s_commonDirs =
        {
            ".",                 // scene folder itself
            "sourceimages",
            "sourceImages",
            "SourceImages",
            "images",
            "Images",
            "textures",
            "Textures",
            "maps",
            "Maps"
        };

        private static readonly string[] s_knownExt =
        {
            ".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".exr", ".hdr"
        };

        private static readonly Regex s_envVar1 = new Regex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);
        private static readonly Regex s_envVar2 = new Regex(@"\$([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        private static readonly Regex s_udimToken = new Regex(@"<\s*udim\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Resolve a Maya texture path into an absolute existing file path if possible.
        /// Returns null if not found.
        /// </summary>
        public static string Resolve(string mayaPath, string scenePath)
        {
            if (string.IsNullOrWhiteSpace(mayaPath)) return null;

            // Normalize slashes and trim quotes
            var p = mayaPath.Trim().Trim('"').Trim('\'').Replace('\\', '/');

            // Expand env vars: ${VAR} and $VAR
            p = ExpandEnvVars(p);

            // UDIM best-effort: replace token with 1001
            p = ReplaceUdimToken(p);

            // Sequence best-effort: if user has %04d, try frame 0001
            p = ReplacePrintfSequence(p);

            // Already absolute?
            if (IsRooted(p))
            {
                var abs = NormalizeAbsolute(p);
                if (File.Exists(abs)) return abs;

                // Try with known extensions if missing
                var alt = TryAppendExtensions(abs);
                if (!string.IsNullOrEmpty(alt)) return alt;

                // Try UDIM globbing if still not found (e.g., path.1001.exr)
                var udim = TryFindAnyUdimTile(abs);
                if (!string.IsNullOrEmpty(udim)) return udim;

                return null;
            }

            // Relative: resolve relative to scene folder and common folders
            var sceneDir = SafeGetDirectory(scenePath);
            if (string.IsNullOrEmpty(sceneDir))
                sceneDir = Directory.GetCurrentDirectory();

            // Try direct relative to scene directory
            {
                var abs0 = Path.GetFullPath(Path.Combine(sceneDir, p));
                abs0 = NormalizeAbsolute(abs0);
                if (File.Exists(abs0)) return abs0;
                var alt0 = TryAppendExtensions(abs0);
                if (!string.IsNullOrEmpty(alt0)) return alt0;
                var udim0 = TryFindAnyUdimTile(abs0);
                if (!string.IsNullOrEmpty(udim0)) return udim0;
            }

            // Try common Maya project folders near the scene
            for (int di = 0; di < s_commonDirs.Length; di++)
            {
                var dir = s_commonDirs[di];
                var abs = Path.GetFullPath(Path.Combine(sceneDir, dir, p));
                abs = NormalizeAbsolute(abs);
                if (File.Exists(abs)) return abs;

                var alt = TryAppendExtensions(abs);
                if (!string.IsNullOrEmpty(alt)) return alt;

                var udim = TryFindAnyUdimTile(abs);
                if (!string.IsNullOrEmpty(udim)) return udim;
            }

            // Try parent folders (a lot of studios put textures in project root/sourceimages)
            // sceneDir/../(common)/p and sceneDir/../../(common)/p
            var parent1 = SafeGetParent(sceneDir, 1);
            var parent2 = SafeGetParent(sceneDir, 2);
            if (!string.IsNullOrEmpty(parent1))
            {
                var found = TryCommonDirs(parent1, p);
                if (!string.IsNullOrEmpty(found)) return found;
            }
            if (!string.IsNullOrEmpty(parent2))
            {
                var found = TryCommonDirs(parent2, p);
                if (!string.IsNullOrEmpty(found)) return found;
            }

            return null;
        }

        private static string TryCommonDirs(string baseDir, string rel)
        {
            for (int di = 0; di < s_commonDirs.Length; di++)
            {
                var dir = s_commonDirs[di];
                var abs = Path.GetFullPath(Path.Combine(baseDir, dir, rel));
                abs = NormalizeAbsolute(abs);
                if (File.Exists(abs)) return abs;

                var alt = TryAppendExtensions(abs);
                if (!string.IsNullOrEmpty(alt)) return alt;

                var udim = TryFindAnyUdimTile(abs);
                if (!string.IsNullOrEmpty(udim)) return udim;
            }
            return null;
        }

        private static string ExpandEnvVars(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            // ${VAR}
            s = s_envVar1.Replace(s, m =>
            {
                var key = m.Groups[1].Value;
                var val = Environment.GetEnvironmentVariable(key);
                return string.IsNullOrEmpty(val) ? m.Value : val.Replace('\\', '/');
            });

            // $VAR
            s = s_envVar2.Replace(s, m =>
            {
                var key = m.Groups[1].Value;
                var val = Environment.GetEnvironmentVariable(key);
                return string.IsNullOrEmpty(val) ? m.Value : val.Replace('\\', '/');
            });

            // Also expand Windows-style %VAR%
            try { s = Environment.ExpandEnvironmentVariables(s); } catch { /* ignore */ }

            return s;
        }

        private static string ReplaceUdimToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (!s_udimToken.IsMatch(s)) return s;
            return s_udimToken.Replace(s, "1001");
        }

        private static string ReplacePrintfSequence(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            // Common patterns:
            //  - %04d
            //  - %d
            // Replace with frame 0001 / 1 as a preview-best-effort.
            if (s.Contains("%04d")) return s.Replace("%04d", "0001");
            if (s.Contains("%d")) return s.Replace("%d", "1");
            return s;
        }

        private static bool IsRooted(string p)
        {
            // Windows: C:/...  or  \\server\share...
            // Unix: /...
            if (string.IsNullOrEmpty(p)) return false;
            if (p.StartsWith("//")) return true;
            if (p.StartsWith("/")) return true;
            return Path.IsPathRooted(p);
        }

        private static string NormalizeAbsolute(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            try
            {
                // Path.GetFullPath normalizes .. and separators
                var abs = Path.GetFullPath(p);
                return abs;
            }
            catch
            {
                return p.Replace('/', Path.DirectorySeparatorChar);
            }
        }

        private static string SafeGetDirectory(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return null;
            try { return Path.GetDirectoryName(scenePath); }
            catch { return null; }
        }

        private static string SafeGetParent(string dir, int levels)
        {
            try
            {
                var d = dir;
                for (int i = 0; i < levels; i++)
                {
                    var p = Directory.GetParent(d);
                    if (p == null) return null;
                    d = p.FullName;
                }
                return d;
            }
            catch { return null; }
        }

        private static string TryAppendExtensions(string absWithoutCheck)
        {
            if (string.IsNullOrEmpty(absWithoutCheck)) return null;

            // If it already has an extension, don't append
            try
            {
                var ext = Path.GetExtension(absWithoutCheck);
                if (!string.IsNullOrEmpty(ext)) return null;
            }
            catch { return null; }

            for (int i = 0; i < s_knownExt.Length; i++)
            {
                var candidate = absWithoutCheck + s_knownExt[i];
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static string TryFindAnyUdimTile(string abs)
        {
            if (string.IsNullOrEmpty(abs)) return null;

            // If file doesn't exist, try searching for *.1001.* style variants
            // Example:
            //  - base: /tex/char_diffuse.exr
            //  - try: /tex/char_diffuse.1001.exr etc
            try
            {
                var dir = Path.GetDirectoryName(abs);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;

                var file = Path.GetFileName(abs);
                if (string.IsNullOrEmpty(file)) return null;

                var name = Path.GetFileNameWithoutExtension(file);
                var ext = Path.GetExtension(file);

                // If no ext, try common ext too
                if (string.IsNullOrEmpty(ext))
                {
                    for (int ei = 0; ei < s_knownExt.Length; ei++)
                    {
                        var found = FindUdimInDir(dir, name, s_knownExt[ei]);
                        if (!string.IsNullOrEmpty(found)) return found;
                    }
                    return null;
                }

                return FindUdimInDir(dir, name, ext);
            }
            catch
            {
                return null;
            }
        }

        private static string FindUdimInDir(string dir, string nameNoExt, string ext)
        {
            // Accept patterns like:
            // name.1001.ext
            // name_1001.ext
            // name1001.ext (less common, but try)
            var patterns = new[]
            {
                $"{nameNoExt}.1???{ext}",
                $"{nameNoExt}_1???{ext}",
                $"{nameNoExt}1???{ext}"
            };

            for (int i = 0; i < patterns.Length; i++)
            {
                var pat = patterns[i];
                var files = Directory.GetFiles(dir, pat);
                if (files != null && files.Length > 0)
                    return files[0];
            }

            return null;
        }
    }
}
