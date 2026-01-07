// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Globalization;
using MayaImporter.Core;
using UnityEngine;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Reads skinCluster weightList from NodeRecord.Attributes (lossless tokens).
    /// Supports single index and simple ranges like ".wl[0:99].w[3]".
    /// Produces BoneWeight (top4 normalized).
    /// </summary>
    public static class MayaSkinWeightReader
    {
        private struct Top4
        {
            public int i0, i1, i2, i3;
            public float w0, w1, w2, w3;

            public void Add(int boneIndex, float w)
            {
                if (w <= 0f) return;

                // If already present, accumulate
                if (i0 == boneIndex) { w0 += w; return; }
                if (i1 == boneIndex) { w1 += w; return; }
                if (i2 == boneIndex) { w2 += w; return; }
                if (i3 == boneIndex) { w3 += w; return; }

                // Insert into sorted slots (descending)
                if (w > w0)
                {
                    i3 = i2; w3 = w2;
                    i2 = i1; w2 = w1;
                    i1 = i0; w1 = w0;
                    i0 = boneIndex; w0 = w;
                    return;
                }
                if (w > w1)
                {
                    i3 = i2; w3 = w2;
                    i2 = i1; w2 = w1;
                    i1 = boneIndex; w1 = w;
                    return;
                }
                if (w > w2)
                {
                    i3 = i2; w3 = w2;
                    i2 = boneIndex; w2 = w;
                    return;
                }
                if (w > w3)
                {
                    i3 = boneIndex; w3 = w;
                }
            }

            public BoneWeight ToBoneWeight()
            {
                float sum = w0 + w1 + w2 + w3;
                if (sum <= 1e-12f) return default;

                float inv = 1f / sum;

                var bw = new BoneWeight
                {
                    boneIndex0 = i0,
                    weight0 = w0 * inv,
                    boneIndex1 = i1,
                    weight1 = w1 * inv,
                    boneIndex2 = i2,
                    weight2 = w2 * inv,
                    boneIndex3 = i3,
                    weight3 = w3 * inv
                };
                return bw;
            }
        }

        public static BoneWeight[] BuildBoneWeights(NodeRecord skinClusterNode, int vertexCount)
        {
            var tops = new Top4[vertexCount];

            // Init indices to -1 so "already present" checks don't collide on 0.
            for (int v = 0; v < vertexCount; v++)
            {
                tops[v].i0 = tops[v].i1 = tops[v].i2 = tops[v].i3 = -1;
                tops[v].w0 = tops[v].w1 = tops[v].w2 = tops[v].w3 = 0f;
            }

            foreach (var kv in skinClusterNode.Attributes)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;

                // Accept common spellings
                // ".wl[...].w[...]"  or ".weightList[...].weights[...]" etc.
                if (!LooksLikeWeightEntry(key))
                    continue;

                // Parse vertex index/range
                if (!TryExtractVertexIndexOrRange(key, out int vStart, out int vEnd))
                    continue;

                // Parse influence index
                if (!TryExtractInfluenceIndex(key, out int infl))
                    continue;

                var val = kv.Value;
                if (val == null || val.ValueTokens == null || val.ValueTokens.Count == 0)
                    continue;

                // If vStart==vEnd -> single value.
                // If range -> ValueTokens should include one value per vertex (best-effort).
                if (vStart == vEnd)
                {
                    if (vStart < 0 || vStart >= vertexCount) continue;
                    if (!TryParseFloat(val.ValueTokens[0], out float w)) continue;
                    tops[vStart].Add(infl, w);
                }
                else
                {
                    int count = Mathf.Min(val.ValueTokens.Count, (vEnd - vStart + 1));
                    for (int i = 0; i < count; i++)
                    {
                        int v = vStart + i;
                        if ((uint)v >= (uint)vertexCount) continue;
                        if (!TryParseFloat(val.ValueTokens[i], out float w)) continue;
                        tops[v].Add(infl, w);
                    }
                }
            }

            var result = new BoneWeight[vertexCount];
            for (int v = 0; v < vertexCount; v++)
                result[v] = tops[v].ToBoneWeight();

            return result;
        }

        private static bool LooksLikeWeightEntry(string key)
        {
            // common
            // .wl[12].w[3]
            // .weightList[12].weights[3]
            if (key.StartsWith(".wl[", StringComparison.Ordinal) && key.Contains(".w[", StringComparison.Ordinal)) return true;
            if (key.StartsWith(".weightList[", StringComparison.Ordinal) && key.Contains(".weights[", StringComparison.Ordinal)) return true;

            // occasionally nested without prefix start
            if (key.Contains(".wl[", StringComparison.Ordinal) && key.Contains(".w[", StringComparison.Ordinal)) return true;
            if (key.Contains("weightList[", StringComparison.Ordinal) && key.Contains("weights[", StringComparison.Ordinal)) return true;

            return false;
        }

        private static bool TryExtractVertexIndexOrRange(string key, out int start, out int end)
        {
            start = end = -1;

            int wl = key.IndexOf(".wl[", StringComparison.Ordinal);
            int basePos = wl >= 0 ? wl + 4 : -1;

            if (basePos < 0)
            {
                int wll = key.IndexOf(".weightList[", StringComparison.Ordinal);
                if (wll >= 0) basePos = wll + ".weightList[".Length;
            }
            if (basePos < 0) return false;

            int rb = key.IndexOf(']', basePos);
            if (rb < 0) return false;

            var inside = key.Substring(basePos, rb - basePos); // "12" or "0:99"
            return ParseIndexOrRange(inside, out start, out end);
        }

        private static bool TryExtractInfluenceIndex(string key, out int infl)
        {
            infl = -1;

            int w = key.IndexOf(".w[", StringComparison.Ordinal);
            int basePos = w >= 0 ? w + 3 : -1;

            if (basePos < 0)
            {
                int ww = key.IndexOf(".weights[", StringComparison.Ordinal);
                if (ww >= 0) basePos = ww + ".weights[".Length;
            }
            if (basePos < 0) return false;

            int rb = key.IndexOf(']', basePos);
            if (rb < 0) return false;

            var inside = key.Substring(basePos, rb - basePos);
            // influence range is not expected; take left if "0:3"
            if (inside.IndexOf(':') >= 0) inside = inside.Split(':')[0];

            return int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out infl);
        }

        private static bool ParseIndexOrRange(string s, out int start, out int end)
        {
            start = end = -1;
            if (string.IsNullOrEmpty(s)) return false;

            int colon = s.IndexOf(':');
            if (colon < 0)
            {
                if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
                end = start;
                return true;
            }

            var a = s.Substring(0, colon);
            var b = s.Substring(colon + 1);
            if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
            if (!int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out end)) return false;

            if (end < start) (start, end) = (end, start);
            return true;
        }

        private static bool TryParseFloat(string s, out float f)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }
    }
}
