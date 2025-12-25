using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// skinCluster のウェイト・インフルエンス名を Maya無し(.ma/.mb)で扱うユーティリティ。
    ///
    /// 100%の根拠:
    /// - Unityが4本制限でも SparseWeight として全ウェイトを保持できる
    /// - ".wl[0:99].w[3]" のような範囲 setAttr も best-effort で展開して欠損を減らす
    /// </summary>
    public static class MayaSkinWeights
    {
        [Serializable]
        public struct SparseWeight
        {
            public int Vertex;
            public int Influence;
            public float Weight;
        }

        /// <summary>
        /// Connections から influenceMatrix[] / matrix[] の index を読んで InfluenceNames[index]=jointName を構築（最重要）。
        /// 取れない場合はフォールバックで leaf名リストを返す（順序保証なし）。
        /// </summary>
        public static string[] CollectInfluenceNamesIndexed(MayaNodeComponentBase skinCluster)
        {
            if (skinCluster == null) return Array.Empty<string>();

            // 1) index 付きで集める（最優先）
            int maxIdx = -1;
            var map = new Dictionary<int, string>();

            if (skinCluster.Connections != null)
            {
                for (int i = 0; i < skinCluster.Connections.Count; i++)
                {
                    var c = skinCluster.Connections[i];
                    if (c == null) continue;

                    // dst が skinCluster 側の想定
                    var dst = c.DstPlug ?? "";
                    if (dst.IndexOf("influenceMatrix[", StringComparison.OrdinalIgnoreCase) < 0 &&
                        dst.IndexOf("matrix[", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    if (!TryExtractBracketIndex(dst, out int idx))
                        continue;

                    var srcNode = c.SrcNodePart;
                    if (string.IsNullOrEmpty(srcNode))
                        srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                    if (string.IsNullOrEmpty(srcNode)) continue;

                    var leaf = MayaPlugUtil.LeafName(srcNode);
                    if (string.IsNullOrEmpty(leaf)) continue;

                    map[idx] = leaf;
                    if (idx > maxIdx) maxIdx = idx;
                }
            }

            if (maxIdx >= 0 && map.Count > 0)
            {
                var arr = new string[maxIdx + 1];
                for (int i = 0; i < arr.Length; i++)
                {
                    if (map.TryGetValue(i, out var name)) arr[i] = name;
                    else arr[i] = ""; // unknown slot
                }
                return arr;
            }

            // 2) fallback: index無し list（順序保証なし）
            var list = CollectInfluenceNamesLoose(skinCluster);
            return list.ToArray();
        }

        /// <summary>
        /// influence 名をゆるく収集（順序保証なし）。
        /// </summary>
        public static List<string> CollectInfluenceNamesLoose(MayaNodeComponentBase skinCluster)
        {
            var list = new List<string>(32);
            if (skinCluster == null) return list;

            // Connections から拾う（確度は高いが index が無いと順序が危うい）
            if (skinCluster.Connections != null)
            {
                for (int i = 0; i < skinCluster.Connections.Count; i++)
                {
                    var c = skinCluster.Connections[i];
                    if (c == null) continue;

                    var dst = c.DstPlug ?? "";
                    if (dst.IndexOf("matrix", StringComparison.OrdinalIgnoreCase) < 0 &&
                        dst.IndexOf("influence", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var srcNode = c.SrcNodePart;
                    if (string.IsNullOrEmpty(srcNode))
                        srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                    if (string.IsNullOrEmpty(srcNode)) continue;

                    var leaf = MayaPlugUtil.LeafName(srcNode);
                    if (!string.IsNullOrEmpty(leaf) && !list.Contains(leaf))
                        list.Add(leaf);
                }
            }

            // Attributes から拾える場合（低確度）
            if (skinCluster.Attributes != null)
            {
                for (int i = 0; i < skinCluster.Attributes.Count; i++)
                {
                    var a = skinCluster.Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                        continue;

                    var k = a.Key;

                    if (k.IndexOf("imn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        k.IndexOf("influence", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        for (int t = 0; t < a.Tokens.Count; t++)
                        {
                            var s = a.Tokens[t];
                            if (LooksLikeNameToken(s) && !list.Contains(s))
                                list.Add(s);
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// wl[vertex].w[influence] / wl[0:99].w[3] を best-effort で SparseWeight[] にする。
        /// - 範囲の場合は Tokens 内の float を列として読み、頂点へ順番に割り当てる
        /// - 単一の場合は Tokens 内の最後の float を採用（setAttr 形式差の吸収）
        /// </summary>
        public static bool TryParseWeightsFromAttributes(
            MayaNodeComponentBase skinCluster,
            out SparseWeight[] weights,
            out int maxVertexIndex,
            out int maxInfluenceIndex)
        {
            weights = Array.Empty<SparseWeight>();
            maxVertexIndex = -1;
            maxInfluenceIndex = -1;

            if (skinCluster == null || skinCluster.Attributes == null || skinCluster.Attributes.Count == 0)
                return false;

            var tmp = new List<SparseWeight>(2048);

            for (int i = 0; i < skinCluster.Attributes.Count; i++)
            {
                var a = skinCluster.Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (!TryParseWeightKey(a.Key, out int vStart, out int vEnd, out int inf))
                    continue;

                // collect float tokens in order
                var floats = CollectFloatsInOrder(a.Tokens);
                if (floats.Count == 0) continue;

                if (vStart == vEnd)
                {
                    // choose last float token (robust against "-type doubleArray N ..." etc)
                    float w = floats[floats.Count - 1];
                    if (w > 0f)
                    {
                        tmp.Add(new SparseWeight { Vertex = vStart, Influence = inf, Weight = w });
                        if (vStart > maxVertexIndex) maxVertexIndex = vStart;
                        if (inf > maxInfluenceIndex) maxInfluenceIndex = inf;
                    }
                }
                else
                {
                    int len = vEnd - vStart + 1;
                    int count = Mathf.Min(len, floats.Count);

                    for (int n = 0; n < count; n++)
                    {
                        float w = floats[n];
                        if (w <= 0f) continue;

                        int vtx = vStart + n;
                        tmp.Add(new SparseWeight { Vertex = vtx, Influence = inf, Weight = w });

                        if (vtx > maxVertexIndex) maxVertexIndex = vtx;
                        if (inf > maxInfluenceIndex) maxInfluenceIndex = inf;
                    }
                }
            }

            if (tmp.Count == 0)
                return false;

            weights = tmp.ToArray();
            return true;
        }

        /// <summary>
        /// Unity BoneWeight(最大4)用に、指定頂点のウェイトを取り出して上位4に圧縮＆正規化。
        /// </summary>
        public static void GetTop4ForVertex(
            SparseWeight[] fullWeights,
            int vertex,
            List<(int inf, float w)> outList)
        {
            outList.Clear();
            if (fullWeights == null || fullWeights.Length == 0) return;

            for (int i = 0; i < fullWeights.Length; i++)
            {
                var sw = fullWeights[i];
                if (sw.Vertex != vertex) continue;
                if (sw.Weight <= 0f) continue;
                outList.Add((sw.Influence, sw.Weight));
            }

            if (outList.Count <= 1) return;

            outList.Sort((a, b) => b.w.CompareTo(a.w));
            if (outList.Count > 4)
                outList.RemoveRange(4, outList.Count - 4);

            float sum = 0f;
            for (int i = 0; i < outList.Count; i++) sum += outList[i].w;
            if (sum > 0f)
            {
                float inv = 1f / sum;
                for (int i = 0; i < outList.Count; i++)
                    outList[i] = (outList[i].inf, outList[i].w * inv);
            }
        }

        // =========================================================
        // Key parsing
        // =========================================================

        private static bool TryParseWeightKey(string key, out int vStart, out int vEnd, out int influence)
        {
            vStart = vEnd = -1;
            influence = -1;
            if (string.IsNullOrEmpty(key)) return false;

            // accept ".wl[...].w[...]" or ".weightList[...].weights[...]"
            // normalize: remove leading dot
            if (key[0] == '.') key = key.Substring(1);

            // vertex part
            if (!TryExtractIndexOrRange(key, "wl[", out vStart, out vEnd) &&
                !TryExtractIndexOrRange(key, "weightList[", out vStart, out vEnd))
                return false;

            // influence part (range not expected; take start if "0:3")
            if (!TryExtractSingleIndex(key, ".w[", out influence) &&
                !TryExtractSingleIndex(key, "w[", out influence) &&
                !TryExtractSingleIndex(key, ".weights[", out influence) &&
                !TryExtractSingleIndex(key, "weights[", out influence))
                return false;

            return vStart >= 0 && vEnd >= vStart && influence >= 0;
        }

        private static bool TryExtractIndexOrRange(string key, string token, out int start, out int end)
        {
            start = end = -1;
            int p = key.IndexOf(token, StringComparison.Ordinal);
            if (p < 0) return false;

            int lb = p + token.Length;
            int rb = key.IndexOf(']', lb);
            if (rb < 0) return false;

            string inside = key.Substring(lb, rb - lb); // "12" or "0:99"
            int colon = inside.IndexOf(':');
            if (colon < 0)
            {
                if (!int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
                    return false;
                end = start;
                return true;
            }
            else
            {
                var a = inside.Substring(0, colon);
                var b = inside.Substring(colon + 1);
                if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
                    return false;
                if (!int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out end))
                    return false;
                if (end < start) (start, end) = (end, start);
                return true;
            }
        }

        private static bool TryExtractSingleIndex(string key, string token, out int idx)
        {
            idx = -1;
            int p = key.IndexOf(token, StringComparison.Ordinal);
            if (p < 0) return false;

            int lb = p + token.Length;
            int rb = key.IndexOf(']', lb);
            if (rb < 0) return false;

            string inside = key.Substring(lb, rb - lb);
            int colon = inside.IndexOf(':');
            if (colon >= 0) inside = inside.Substring(0, colon);

            return int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        }

        private static bool TryExtractBracketIndex(string s, out int idx)
        {
            idx = -1;
            if (string.IsNullOrEmpty(s)) return false;

            int lb = s.LastIndexOf('[');
            int rb = s.LastIndexOf(']');
            if (lb < 0 || rb < 0 || rb <= lb) return false;

            var inside = s.Substring(lb + 1, rb - lb - 1);
            int colon = inside.IndexOf(':');
            if (colon >= 0) inside = inside.Substring(0, colon);

            return int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        }

        // =========================================================
        // Tokens -> floats
        // =========================================================

        private static List<float> CollectFloatsInOrder(List<string> tokens)
        {
            var list = new List<float>(tokens.Count);
            for (int i = 0; i < tokens.Count; i++)
            {
                if (float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    list.Add(f);
            }
            return list;
        }

        private static bool LooksLikeNameToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (char.IsDigit(s[0])) return false;
            if (s == "setAttr" || s == "-type") return false;
            return true;
        }
    }
}
