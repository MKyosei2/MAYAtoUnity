using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MayaImporter.Core
{
    public static class MayaMaterialLibrary
    {
        private static readonly Dictionary<string, UnityEngine.Material> _cache =
            new Dictionary<string, UnityEngine.Material>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Texture2D> _diskTexCache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private static UnityEngine.Shader _standard;

        public static UnityEngine.Material GetOrCreateStandard(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "MayaMaterial";

            if (_cache.TryGetValue(key, out var mat) && mat != null)
                return mat;

            if (_standard == null)
                _standard = UnityEngine.Shader.Find("Standard");

            mat = (_standard != null)
                ? new UnityEngine.Material(_standard)
                : new UnityEngine.Material(UnityEngine.Shader.Find("Hidden/InternalErrorShader"));

            mat.name = key;
            _cache[key] = mat;
            return mat;
        }

        public static void ConfigureStandard(UnityEngine.Material mat, string baseKey, NodeRecord meshOrShapeRec, int subIndex)
        {
            if (mat == null) return;

            var key = $"{baseKey}__sub{subIndex}";
            var lc = key.ToLowerInvariant();

            var col = HashToColor(key);
            float alpha = 1f;

            // transparency name-hints
            if (ContainsAny(lc, "glass", "transparent", "translucent", "alpha", "opacity"))
                alpha = 0.35f;

            // unified + mb legacy hints
            if (TryGetHint(meshOrShapeRec, ".materialName", out var matName) ||
                TryGetHint(meshOrShapeRec, ".mbMaterialName", out matName))
            {
                var mlc = (matName ?? "").ToLowerInvariant();
                if (ContainsAny(mlc, "glass", "transparent", "translucent", "alpha", "opacity"))
                    alpha = 0.35f;
                if (ContainsAny(mlc, "metal", "chrome", "iron", "steel", "gold", "silver"))
                    SetMetallicSmoothness(mat, metallic: 1f, smoothness: 0.75f);
            }

            if (TryGetHint(meshOrShapeRec, ".shadingGroupName", out var sgName) ||
                TryGetHint(meshOrShapeRec, ".mbShadingGroupName", out sgName))
            {
                var slc = (sgName ?? "").ToLowerInvariant();
                if (ContainsAny(slc, "metal", "chrome", "iron", "steel", "gold", "silver"))
                    SetMetallicSmoothness(mat, metallic: 1f, smoothness: 0.75f);
            }

            if (ContainsAny(lc, "metal", "chrome", "iron", "steel", "gold", "silver"))
                SetMetallicSmoothness(mat, metallic: 1f, smoothness: 0.75f);
            if (ContainsAny(lc, "rough", "matte"))
                SetMetallicSmoothness(mat, metallic: 0f, smoothness: 0.15f);

            col.a = alpha;

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", col);

            // ★ここが本実装：hint から Resources だけじゃなく “ディスクから” も読む
            var tex = TryLoadTextureFromHints(meshOrShapeRec);
            if (tex != null && mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);

            if (alpha < 0.999f) SetStandardTransparent(mat);
            else SetStandardOpaque(mat);
        }

        public static void Clear()
        {
            _cache.Clear();
            _diskTexCache.Clear();
            _standard = null;
        }

        // -----------------------
        // Helpers
        // -----------------------

        private static bool TryGetHint(NodeRecord rec, string key, out string value)
        {
            value = null;
            if (rec?.Attributes == null) return false;
            if (!rec.Attributes.TryGetValue(key, out var raw) || raw == null) return false;
            if (raw.ValueTokens == null || raw.ValueTokens.Count == 0) return false;
            value = raw.ValueTokens[0];
            return !string.IsNullOrEmpty(value);
        }

        private static Texture2D TryLoadTextureFromHints(NodeRecord rec)
        {
            if (rec?.Attributes == null) return null;

            // unified first, then mb legacy
            var prefixes = new[] { ".textureHint", ".mbTextureHint" };

            for (int p = 0; p < prefixes.Length; p++)
            {
                var prefix = prefixes[p];
                for (int i = 1; i <= 4; i++)
                {
                    var key = (i == 1) ? prefix : prefix + i;
                    if (!TryGetHint(rec, key, out var s) || string.IsNullOrEmpty(s)) continue;

                    // 1) Resources (旧仕様)
                    var stem = FileStem(s);
                    if (!string.IsNullOrEmpty(stem))
                    {
                        var texR = Resources.Load<Texture2D>(stem);
                        if (texR != null) return texR;
                    }

                    // 2) Disk resolve (新仕様：sourceimages 等を探す)
                    var scenePath = MayaBuildContext.CurrentScene?.SourcePath;
                    var abs = MayaTexturePathResolver.Resolve(s, scenePath);
                    if (string.IsNullOrEmpty(abs)) continue;

                    var texD = LoadTextureFromDisk(abs);
                    if (texD != null) return texD;
                }
            }

            return null;
        }

        private static Texture2D LoadTextureFromDisk(string absPath)
        {
            if (string.IsNullOrEmpty(absPath)) return null;

            if (_diskTexCache.TryGetValue(absPath, out var cached) && cached != null)
                return cached;

            try
            {
                if (!File.Exists(absPath)) return null;

                var bytes = File.ReadAllBytes(absPath);
                if (bytes == null || bytes.Length == 0) return null;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                if (!tex.LoadImage(bytes, markNonReadable: false))
                    return null;

                tex.name = Path.GetFileNameWithoutExtension(absPath);
                _diskTexCache[absPath] = tex;
                return tex;
            }
            catch
            {
                return null;
            }
        }

        private static string FileStem(string pathOrFile)
        {
            if (string.IsNullOrEmpty(pathOrFile)) return null;

            var s = pathOrFile.Replace('\\', '/');
            int slash = s.LastIndexOf('/');
            if (slash >= 0 && slash < s.Length - 1) s = s.Substring(slash + 1);

            int dot = s.LastIndexOf('.');
            if (dot > 0) s = s.Substring(0, dot);

            return s.Length < 1 ? null : s;
        }

        private static void SetMetallicSmoothness(Material mat, float metallic, float smoothness)
        {
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        }

        private static bool ContainsAny(string s, params string[] tokens)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < tokens.Length; i++)
                if (s.Contains(tokens[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static Color HashToColor(string key)
        {
            unchecked
            {
                int h = 17;
                for (int i = 0; i < key.Length; i++)
                    h = h * 31 + key[i];

                float r = 0.2f + ((h >> 0) & 0xFF) / 255f * 0.7f;
                float g = 0.2f + ((h >> 8) & 0xFF) / 255f * 0.7f;
                float b = 0.2f + ((h >> 16) & 0xFF) / 255f * 0.7f;
                return new Color(r, g, b, 1f);
            }
        }

        private static void SetStandardOpaque(Material material)
        {
            if (material == null) return;
            material.SetFloat("_Mode", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = -1;
        }

        private static void SetStandardTransparent(Material material)
        {
            if (material == null) return;
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
