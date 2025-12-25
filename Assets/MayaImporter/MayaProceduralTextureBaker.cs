// Assets/MayaImporter/MayaProceduralTextureBaker.cs
// Procedural/utility texture baking + graph helpers (Unity-only, Maya不要)
// 互換APIを全部ここに集約：
// - TryCollectRemapCurve(node, "red", out curve) など 3引数版
// - TryLoadTextureFromNodeName
// - TryReadFloatAttr / TryReadColorAttr / TryReadStringAttr
// - BakeChecker / BakeNoise / BakeRamp / BakeGamma / BakeBrightnessContrast / BakeLuminance
// - BuildCachePath / SaveAsPng / LoadPngOrNull
// - BakeToPng(pixelFunc)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;
using MayaImporter.Components;

namespace MayaImporter.Shading
{
    public static class MayaProceduralTextureBaker
    {
        // =========================================================
        // Paths / IO
        // =========================================================

        public static string BuildCachePath(string nodeType, string nodeName, string uuid, int width, int height)
        {
            string Safe(string s)
            {
                if (string.IsNullOrEmpty(s)) return "null";
                foreach (var c in Path.GetInvalidFileNameChars())
                    s = s.Replace(c, '_');
                return s;
            }

            var dir = Path.Combine(Application.persistentDataPath, "MayaImporter", "BakedProceduralTextures");
            Directory.CreateDirectory(dir);

            var file = $"{Safe(nodeType)}__{Safe(nodeName)}__{Safe(uuid)}__{width}x{height}.png";
            return Path.Combine(dir, file);
        }

        public static bool SaveAsPng(Texture2D tex, string absPath)
        {
            if (tex == null || string.IsNullOrEmpty(absPath)) return false;

            try
            {
                var dir = Path.GetDirectoryName(absPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllBytes(absPath, tex.EncodeToPNG());
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static Texture2D LoadPngOrNull(string absPath)
        {
            try
            {
                if (string.IsNullOrEmpty(absPath) || !File.Exists(absPath)) return null;
                var bytes = File.ReadAllBytes(absPath);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                if (!t.LoadImage(bytes)) return null;
                t.name = Path.GetFileNameWithoutExtension(absPath);
                return t;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Remap/Reverse/Layered が使う “ピクセル関数→PNG出力” 互換API
        /// </summary>
        public static string BakeToPng(
            MonoBehaviour owner,
            string bakeId,
            int width,
            int height,
            Func<int, int, Color> pixelFunc,
            MayaImportLog log)
        {
            width = Mathf.Clamp(width <= 0 ? 256 : width, 2, 4096);
            height = Mathf.Clamp(height <= 0 ? 256 : height, 2, 4096);

            string nodeType = owner != null ? owner.GetType().Name : "UnknownNode";
            string nodeName = owner != null ? owner.gameObject.name : "UnknownGO";
            string uuid = owner != null ? (owner.GetComponent<MayaNodeComponentBase>()?.Uuid ?? "") : "";

            var path = BuildCachePath(nodeType, $"{nodeName}__{bakeId}", uuid, width, height);

            // deterministic: if exists, reuse
            if (File.Exists(path))
                return path;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixelFunc != null ? pixelFunc(x, y) : Color.magenta;

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            if (!SaveAsPng(tex, path))
            {
                log?.Warn($"[Baker] Failed to write png: {path}");
                return null;
            }

            log?.Info($"[Baker] Baked: {path}");
            return path;
        }

        // =========================================================
        // Graph / attribute helpers (互換)
        // =========================================================

        public static bool TryLoadTextureFromNodeName(
            string nodeNameOrDag,
            out Texture2D tex,
            out MayaTextureMetadata texMeta,
            MayaImportLog log)
        {
            tex = null;
            texMeta = null;
            if (string.IsNullOrEmpty(nodeNameOrDag)) return false;

            var tr = MayaNodeLookup.FindTransform(nodeNameOrDag);
            if (tr == null) return false;

            // 1) Prefer metadata-driven baked/result path
            texMeta = tr.GetComponent<MayaTextureMetadata>();
            if (texMeta != null && !string.IsNullOrEmpty(texMeta.fileTextureName))
            {
                var p = texMeta.fileTextureName.Trim().Trim('"');
                tex = LoadImageSmart(p, log);
                if (tex != null) return true;
            }

            // 2) Fallback: raw attr fileTextureName / ftn
            var node = tr.GetComponent<MayaNodeComponentBase>();
            if (node != null)
            {
                if (TryReadStringAttr(node, new[] { "fileTextureName", ".fileTextureName", "ftn", ".ftn" }, out var raw) &&
                    !string.IsNullOrEmpty(raw))
                {
                    tex = LoadImageSmart(raw.Trim().Trim('"'), log);
                    if (tex != null) return true;
                }
            }

            return false;
        }

        public static bool TryReadStringAttr(MayaNodeComponentBase node, string[] keys, out string value)
        {
            value = null;
            if (node == null || keys == null) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(node, keys[i], out var a) && a?.Tokens != null && a.Tokens.Count > 0)
                {
                    value = a.Tokens[a.Tokens.Count - 1];
                    return true;
                }
            }
            return false;
        }

        public static bool TryReadFloatAttr(MayaNodeComponentBase node, string[] keys, out float f)
        {
            f = 0f;
            if (node == null || keys == null) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(node, keys[i], out var a) && a?.Tokens != null && a.Tokens.Count > 0)
                {
                    for (int t = a.Tokens.Count - 1; t >= 0; t--)
                    {
                        if (TryF(a.Tokens[t], out f)) return true;
                    }
                }
            }
            return false;
        }

        public static bool TryReadColorAttr(MayaNodeComponentBase node, string[] keys, out Color c)
        {
            c = Color.white;
            if (node == null || keys == null) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(node, keys[i], out var a) && a?.Tokens != null && a.Tokens.Count >= 3)
                {
                    if (TryF(a.Tokens[0], out var r) &&
                        TryF(a.Tokens[1], out var g) &&
                        TryF(a.Tokens[2], out var b))
                    {
                        c = new Color(r, g, b, 1f);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryGetAttr(MayaNodeComponentBase node, string key, out MayaNodeComponentBase.SerializedAttribute attr)
        {
            attr = null;
            if (node == null || node.Attributes == null || string.IsNullOrEmpty(key)) return false;

            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, key, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            var dot = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : "." + key;
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, dot, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            return false;
        }

        private static Texture2D LoadImageSmart(string pathOrFile, MayaImportLog log)
        {
            if (string.IsNullOrEmpty(pathOrFile)) return null;

            var raw = pathOrFile.Trim().Trim('"');

            // absolute
            if (File.Exists(raw))
            {
                // baked or real image
                return LoadImageFile(raw);
            }

            // relative to scene dir
            var scene = MayaBuildContext.CurrentScene;
            var scenePath = scene != null ? scene.SourcePath : null;
            if (!string.IsNullOrEmpty(scenePath))
            {
                var dir = Path.GetDirectoryName(scenePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var combined = Path.Combine(dir, raw);
                    if (File.Exists(combined))
                        return LoadImageFile(combined);
                }
            }

            // relative to baked folder
            var bakedCandidate = Path.Combine(Application.persistentDataPath, "MayaImporter", "BakedProceduralTextures", raw);
            if (File.Exists(bakedCandidate))
                return LoadImageFile(bakedCandidate);

            log?.Warn($"[Baker] texture not found: '{pathOrFile}'");
            return null;
        }

        private static Texture2D LoadImageFile(string absPath)
        {
            try
            {
                var bytes = File.ReadAllBytes(absPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
                if (!tex.LoadImage(bytes)) return null;
                tex.name = Path.GetFileNameWithoutExtension(absPath);
                return tex;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryF(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        // =========================================================
        // Remap curves (互換：3引数版)
        // =========================================================

        public struct RemapKey
        {
            public float pos;     // 0..1
            public float val;     // typically 0..1
            public int interp;    // 1=linear, 2=smooth(best-effort)

            // 互換: 他実装が value を参照しても通る
            public float value { get => val; set => val = value; }
        }

        /// <summary>
        /// 互換: TryCollectRemapCurve(node, "red", out curve)
        /// red/green/blue/value/hue/saturation/value など channelPrefix に対応。
        /// </summary>
        public static bool TryCollectRemapCurve(MayaNodeComponentBase node, string channelPrefix, out List<RemapKey> curve)
        {
            curve = null;
            if (node == null || string.IsNullOrEmpty(channelPrefix) || node.Attributes == null) return false;

            var pos = new Dictionary<int, float>();
            var val = new Dictionary<int, float>();
            var itp = new Dictionary<int, int>();

            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0) continue;

                var k = a.Key;

                if (k.IndexOf(channelPrefix + "[", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!TryParseBracketIndex(k, out var idx))
                    continue;

                bool isPos = k.IndexOf("Position", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isVal = k.IndexOf("FloatValue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             (k.IndexOf("Value", StringComparison.OrdinalIgnoreCase) >= 0 && !isPos);
                bool isInterp = k.IndexOf("Interp", StringComparison.OrdinalIgnoreCase) >= 0;

                var last = a.Tokens[a.Tokens.Count - 1];

                if (isPos && TryF(last, out var p)) pos[idx] = p;
                else if (isInterp && int.TryParse(last.Trim(), out var ip)) itp[idx] = ip;
                else if (isVal && TryF(last, out var v)) val[idx] = v;
            }

            var list = new List<RemapKey>(pos.Count);
            foreach (var kv in pos)
            {
                int idx = kv.Key;
                float p = Mathf.Clamp01(kv.Value);
                float v = val.TryGetValue(idx, out var vv) ? vv : p;
                int ip = itp.TryGetValue(idx, out var ii) ? ii : 1;

                list.Add(new RemapKey { pos = p, val = v, interp = ip });
            }

            if (list.Count == 0) return false;

            list.Sort((a, b) => a.pos.CompareTo(b.pos));

            // endpoints
            if (list[0].pos > 0f)
                list.Insert(0, new RemapKey { pos = 0f, val = list[0].val, interp = list[0].interp });
            if (list[list.Count - 1].pos < 1f)
            {
                var last = list[list.Count - 1];
                list.Add(new RemapKey { pos = 1f, val = last.val, interp = last.interp });
            }

            curve = list;
            return true;
        }

        public static float EvalRemapCurve(List<RemapKey> curve, float t)
        {
            if (curve == null || curve.Count == 0) return t;
            t = Mathf.Clamp01(t);

            for (int i = 0; i < curve.Count - 1; i++)
            {
                var a = curve[i];
                var b = curve[i + 1];
                if (t < a.pos) continue;
                if (t > b.pos) continue;

                float span = Mathf.Max(1e-6f, b.pos - a.pos);
                float u = Mathf.Clamp01((t - a.pos) / span);

                if (a.interp == 2 || b.interp == 2)
                    u = u * u * (3f - 2f * u); // smoothstep

                return Mathf.Clamp01(Mathf.Lerp(a.val, b.val, u));
            }

            if (t <= curve[0].pos) return Mathf.Clamp01(curve[0].val);
            return Mathf.Clamp01(curve[curve.Count - 1].val);
        }

        private static bool TryParseBracketIndex(string key, out int idx)
        {
            idx = -1;
            int lb = key.IndexOf('[');
            int rb = key.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = key.Substring(lb + 1, rb - lb - 1);
            int colon = inner.IndexOf(':');
            if (colon >= 0) inner = inner.Substring(0, colon);

            return int.TryParse(inner, out idx);
        }

        // =========================================================
        // UV helpers
        // =========================================================

        private static Vector2 ApplyUvTransform(Vector2 uv01, Vector2 repeat, Vector2 offset, float rotateDeg)
        {
            var uv = new Vector2(uv01.x * repeat.x + offset.x, uv01.y * repeat.y + offset.y);

            if (Mathf.Abs(rotateDeg) > 0.0001f)
            {
                float rad = rotateDeg * Mathf.Deg2Rad;
                float s = Mathf.Sin(rad);
                float c = Mathf.Cos(rad);

                uv -= new Vector2(0.5f, 0.5f);
                float x = uv.x * c - uv.y * s;
                float y = uv.x * s + uv.y * c;
                uv = new Vector2(x, y) + new Vector2(0.5f, 0.5f);
            }

            return uv;
        }

        private static float Frac(float v) => v - Mathf.Floor(v);

        // =========================================================
        // Bake primitives (Texture2D outputs)
        // =========================================================

        public struct RampEntry
        {
            public float position;
            public Color color;
        }

        public static Texture2D BakeChecker(
            int width, int height,
            Color c1, Color c2,
            Vector2 repeatUV, Vector2 offsetUV, float rotateUVDegrees,
            int cellsU, int cellsV)
        {
            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);
            cellsU = Mathf.Max(1, cellsU);
            cellsV = Mathf.Max(1, cellsV);

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v01 = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u01 = (x + 0.5f) / width;
                    var uv = ApplyUvTransform(new Vector2(u01, v01), repeatUV, offsetUV, rotateUVDegrees);

                    float u = Frac(uv.x);
                    float v = Frac(uv.y);

                    int iu = Mathf.FloorToInt(u * cellsU);
                    int iv = Mathf.FloorToInt(v * cellsV);
                    bool odd = ((iu + iv) & 1) == 1;

                    pixels[y * width + x] = odd ? c2 : c1;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        public static Texture2D BakeNoise(
            int width, int height,
            Color colorA, Color colorB,
            float frequency,
            Vector2 repeatUV, Vector2 offsetUV, float rotateUVDegrees,
            int seed)
        {
            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);
            frequency = Mathf.Max(0.0001f, frequency);

            float sx = (seed * 0.1234f) % 97f;
            float sy = (seed * 0.9876f) % 53f;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v01 = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u01 = (x + 0.5f) / width;
                    var uv = ApplyUvTransform(new Vector2(u01, v01), repeatUV, offsetUV, rotateUVDegrees);

                    float n = Mathf.PerlinNoise(uv.x * frequency + sx, uv.y * frequency + sy);
                    pixels[y * width + x] = Color.Lerp(colorA, colorB, n);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        public static Texture2D BakeRamp(
            int width, int height,
            List<RampEntry> entries,
            int rampType,
            Vector2 repeatUV, Vector2 offsetUV, float rotateUVDegrees)
        {
            width = Mathf.Max(2, width);
            height = Mathf.Max(2, height);

            if (entries == null || entries.Count == 0)
            {
                entries = new List<RampEntry>
                {
                    new RampEntry{ position = 0f, color = Color.black },
                    new RampEntry{ position = 1f, color = Color.white },
                };
            }

            entries.Sort((a, b) => a.position.CompareTo(b.position));

            float EvalT(Vector2 uv)
            {
                switch (rampType)
                {
                    case 1: return uv.x;                       // horizontal
                    case 2: return (uv.x + uv.y) * 0.5f;       // diagonal
                    case 3:                                    // radial
                        var d = uv - new Vector2(0.5f, 0.5f);
                        return Mathf.Clamp01(d.magnitude * 2f);
                    case 0:
                    default: return uv.y;                      // vertical
                }
            }

            Color Sample(float t)
            {
                t = Mathf.Clamp01(t);

                if (t <= entries[0].position) return entries[0].color;
                if (t >= entries[entries.Count - 1].position) return entries[entries.Count - 1].color;

                for (int i = 0; i < entries.Count - 1; i++)
                {
                    var a = entries[i];
                    var b = entries[i + 1];
                    if (t >= a.position && t <= b.position)
                    {
                        float u = Mathf.InverseLerp(a.position, b.position, t);
                        return Color.Lerp(a.color, b.color, u);
                    }
                }

                return entries[entries.Count - 1].color;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                float v01 = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u01 = (x + 0.5f) / width;

                    var uv = ApplyUvTransform(new Vector2(u01, v01), repeatUV, offsetUV, rotateUVDegrees);
                    uv.x = Mathf.Clamp01(uv.x);
                    uv.y = Mathf.Clamp01(uv.y);

                    pixels[y * width + x] = Sample(EvalT(uv));
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        public static Texture2D BakeGamma(Texture2D src, float gamma)
        {
            if (src == null) return null;
            gamma = Mathf.Max(0.0001f, gamma);
            float inv = 1f / gamma;

            var w = src.width;
            var h = src.height;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);

            var sp = src.GetPixels();
            var dp = new Color[sp.Length];

            for (int i = 0; i < sp.Length; i++)
            {
                var c = sp[i];
                c.r = Mathf.Pow(Mathf.Clamp01(c.r), inv);
                c.g = Mathf.Pow(Mathf.Clamp01(c.g), inv);
                c.b = Mathf.Pow(Mathf.Clamp01(c.b), inv);
                dp[i] = c;
            }

            dst.SetPixels(dp);
            dst.Apply(false, false);
            return dst;
        }

        public static Texture2D BakeBrightnessContrast(Texture2D src, float brightness, float contrast)
        {
            if (src == null) return null;
            contrast = Mathf.Max(0f, contrast);

            var w = src.width;
            var h = src.height;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);

            var sp = src.GetPixels();
            var dp = new Color[sp.Length];

            for (int i = 0; i < sp.Length; i++)
            {
                var c = sp[i];
                c.r = Mathf.Clamp01((c.r - 0.5f) * contrast + 0.5f + brightness);
                c.g = Mathf.Clamp01((c.g - 0.5f) * contrast + 0.5f + brightness);
                c.b = Mathf.Clamp01((c.b - 0.5f) * contrast + 0.5f + brightness);
                dp[i] = c;
            }

            dst.SetPixels(dp);
            dst.Apply(false, false);
            return dst;
        }

        public static Texture2D BakeLuminance(Texture2D src)
        {
            if (src == null) return null;

            var w = src.width;
            var h = src.height;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);

            var sp = src.GetPixels();
            var dp = new Color[sp.Length];

            for (int i = 0; i < sp.Length; i++)
            {
                var c = sp[i];
                float yv = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                dp[i] = new Color(yv, yv, yv, c.a);
            }

            dst.SetPixels(dp);
            dst.Apply(false, false);
            return dst;
        }
    }
}
