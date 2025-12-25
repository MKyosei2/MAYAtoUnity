using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MayaImporter.Utils
{
    /// <summary>
    /// file ノード等のテクスチャを “Unityだけ” でロードする。
    /// Editor/Runtime 両対応（UnityEngine.ImageConversion 使用）。
    /// </summary>
    public static class TextureLoader
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D LoadTexture(string absolutePath, bool useCache = true)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;
            absolutePath = StringParsingUtil.NormalizeSlashes(absolutePath);

            if (useCache && Cache.TryGetValue(absolutePath, out var cached) && cached != null)
                return cached;

            if (!File.Exists(absolutePath))
                return null;

            byte[] bytes;
            try { bytes = File.ReadAllBytes(absolutePath); }
            catch { return null; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
            if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
            {
                Object.Destroy(tex);
                return null;
            }

            tex.name = Path.GetFileName(absolutePath);

            if (useCache) Cache[absolutePath] = tex;
            return tex;
        }

        public static void ClearCache()
        {
            foreach (var kv in Cache)
            {
                if (kv.Value != null)
                    Object.Destroy(kv.Value);
            }
            Cache.Clear();
        }
    }
}
