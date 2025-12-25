using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase B-4:
    /// Maya側の Metallic/Roughness を Unity向けに吸収。
    ///
    /// 出力は “MetallicSmoothness packed”:
    /// - R = metallic
    /// - A = smoothness (1 - roughness)
    ///
    /// 入力:
    /// - metallicTex: metallic を含むテクスチャ（R参照）
    /// - roughnessTex: roughness を含むテクスチャ（R参照）
    /// - metallicScalar / smoothnessScalar: テクスチャが無い場合のスカラー補完
    ///
    /// 失敗しても null を返す（importを止めない）
    /// </summary>
    public static class MayaTexturePacker
    {
        private static readonly Dictionary<string, Texture2D> s_cache = new Dictionary<string, Texture2D>();

        public static Texture2D BuildMetallicSmoothnessPacked_BestEffort(
            Texture2D metallicTex,
            Texture2D roughnessTex,
            float metallicScalar,
            float smoothnessScalar,
            MayaImportLog log)
        {
            log ??= new MayaImportLog();

            // no need to pack if only metallicTex and it likely already has alpha smoothness
            // but we can't trust it; still, if no roughness and metallicTex has alpha, we can just use it.
            if (roughnessTex == null && metallicTex != null)
            {
                // We could pass-through (best-effort)
                return metallicTex;
            }

            // build cache key
            var key = $"PACK_MS__m={(metallicTex ? metallicTex.GetInstanceID() : 0)}__r={(roughnessTex ? roughnessTex.GetInstanceID() : 0)}__ms={metallicScalar:0.###}__ss={smoothnessScalar:0.###}";
            if (s_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            // determine size
            var w = 0;
            var h = 0;

            if (metallicTex != null) { w = metallicTex.width; h = metallicTex.height; }
            if (roughnessTex != null)
            {
                if (w == 0) { w = roughnessTex.width; h = roughnessTex.height; }
                else
                {
                    // mismatch -> choose min to reduce readback cost
                    w = Math.Min(w, roughnessTex.width);
                    h = Math.Min(h, roughnessTex.height);
                }
            }

            if (w <= 0 || h <= 0)
            {
                log.Warn("[TexturePacker] invalid size for packing metallic/smoothness.");
                return null;
            }

            try
            {
                // read pixels (requires readable textures; for Project textures importer usually makes readable=false)
                // We can still pack best-effort using GetPixelBilinear if readable; if not readable -> fallback to scalar.
                var outTex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true, linear: true);
                outTex.name = "MayaPacked_MetallicSmoothness";

                var colors = new Color32[w * h];

                bool mReadable = metallicTex != null && metallicTex.isReadable;
                bool rReadable = roughnessTex != null && roughnessTex.isReadable;

                // If not readable, we still pack scalar-only to ensure deterministic output
                if ((metallicTex != null && !mReadable) || (roughnessTex != null && !rReadable))
                {
                    log.Info("[TexturePacker] Source texture not readable; packing using scalar fallback (enable Read/Write on source to improve).");
                }

                for (int y = 0; y < h; y++)
                {
                    float v = (h <= 1) ? 0f : (float)y / (h - 1);
                    for (int x = 0; x < w; x++)
                    {
                        float u = (w <= 1) ? 0f : (float)x / (w - 1);

                        float m = metallicScalar;
                        float r = 1f - smoothnessScalar; // roughness

                        if (metallicTex != null && mReadable)
                            m = metallicTex.GetPixelBilinear(u, v).r;

                        if (roughnessTex != null && rReadable)
                            r = roughnessTex.GetPixelBilinear(u, v).r;

                        float smooth = Mathf.Clamp01(1f - r);

                        // R=metallic, A=smoothness
                        // G/B unused but keep 0
                        var c = new Color(m, 0f, 0f, smooth);
                        colors[y * w + x] = (Color32)c;
                    }
                }

                outTex.SetPixels32(colors);
                outTex.Apply(updateMipmaps: true, makeNoLongerReadable: false);

                s_cache[key] = outTex;
                return outTex;
            }
            catch (Exception e)
            {
                log.Warn("[TexturePacker] Exception: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }
    }
}
