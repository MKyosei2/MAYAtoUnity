using System;
using System.Collections.Generic;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Meshの「Mayaデータとして存在するが Unity Mesh では表現制限がある/実装差が出る」点を監査する。
    ///
    /// 重要方針(ポートフォリオ用100%):
    /// - ここで Blocker を安易に出さない
    /// - Unityに概念が無い場合でも「RawTokensとして保持されている」＝データ欠損ではない
    /// - 視覚差/再現アルゴリズム差は Warn として可視化
    /// </summary>
    public static class MayaMeshLimitationsReporter
    {
        public sealed class MeshLimitationRow
        {
            public string MeshNodeName;
            public string IssueKey;
            public string Severity; // Info / Warn
            public string Details;
        }

        public static List<MeshLimitationRow> Collect(MayaSceneData scene)
        {
            var outList = new List<MeshLimitationRow>(128);
            if (scene == null || scene.Nodes == null) return outList;

            foreach (var kv in scene.Nodes)
            {
                var node = kv.Value;
                if (node == null) continue;
                if (!string.Equals(node.NodeType, "mesh", StringComparison.Ordinal)) continue;

                CollectForMeshNode(node, outList);
            }

            return outList;
        }

        private static void CollectForMeshNode(NodeRecord mesh, List<MeshLimitationRow> outList)
        {
            // ===== Gather quick stats =====
            int vertCount = EstimateIndexedArrayMaxPlusOne(mesh, prefixes: new[] { ".vt", "vt", ".pt", "pt", ".pnts", "pnts" });
            int normalPoolCount = EstimateIndexedArrayMaxPlusOne(mesh, prefixes: new[] { ".n", "n" });

            int uvSetMaxInAttrs = FindMaxUvSetIndexInAttributes(mesh);
            bool hasHolesToken = ScanPolyFacesForHoles(mesh, out int maxFaceCornerCount, out int uvSetMaxInFaces, out bool hasFaceVaryingNormalTokens);

            int uvSetMax = Math.Max(uvSetMaxInAttrs, uvSetMaxInFaces);
            int uvSetCount = (uvSetMax >= 0) ? (uvSetMax + 1) : 0;

            // ===== Triangulation differences =====
            if (maxFaceCornerCount > 3)
            {
                outList.Add(new MeshLimitationRow
                {
                    MeshNodeName = mesh.Name,
                    IssueKey = "MeshTriangulation_Fan",
                    Severity = "Warn",
                    Details =
                        $"Poly faces detected (maxCorners={maxFaceCornerCount}). Current reconstruction uses fan triangulation by default; concave polygons/custom triangulation may differ. (Raw polyFaces preserved.)"
                });
            }

            // ===== Holes =====
            if (hasHolesToken)
            {
                outList.Add(new MeshLimitationRow
                {
                    MeshNodeName = mesh.Name,
                    IssueKey = "MeshFaceHoles_Preserved",
                    Severity = "Warn",
                    Details =
                        "polyFaces contains hole tokens ('h'). Unity Mesh has no direct hole concept. Tool preserves the data in raw tokens; visual mesh may be built from outer loop only unless a hole-triangulation solver is added."
                });
            }

            // ===== UV sets =====
            if (uvSetCount > 8)
            {
                outList.Add(new MeshLimitationRow
                {
                    MeshNodeName = mesh.Name,
                    IssueKey = "MeshUVSets_Over8",
                    Severity = "Warn",
                    Details =
                        $"UV sets detected: {uvSetCount} (maxIndex={uvSetMax}). Unity Mesh supports up to 8 UV channels; sets beyond 8 remain preserved in raw attributes but are not applied to Mesh channels by default."
                });
            }
            else if (uvSetCount > 4)
            {
                outList.Add(new MeshLimitationRow
                {
                    MeshNodeName = mesh.Name,
                    IssueKey = "MeshUVSets_Over4",
                    Severity = "Info",
                    Details =
                        $"UV sets detected: {uvSetCount}. Tool supports applying up to 8 channels. (Raw UV sets preserved regardless.)"
                });
            }

            // ===== Face-varying normals =====
            if (hasFaceVaryingNormalTokens)
            {
                outList.Add(new MeshLimitationRow
                {
                    MeshNodeName = mesh.Name,
                    IssueKey = "MeshNormals_FaceVarying",
                    Severity = "Info",
                    Details =
                        "polyFaces contains face-varying normal mapping tokens (mn/mf). Tool expands vertices per-corner and applies normals when normal pool exists; minor differences can remain depending on Maya lock/soft-hard edge semantics."
                });
            }
            else if (normalPoolCount > 0 && vertCount > 0 && normalPoolCount != vertCount)
            {
                outList.Add(new MeshLimitationRow
                {
                    MeshNodeName = mesh.Name,
                    IssueKey = "MeshNormals_PoolCountMismatch",
                    Severity = "Warn",
                    Details =
                        $"Normal pool count={normalPoolCount}, vertex count≈{vertCount}. Maya may be storing normals in a separate pool; without mapping tokens, exact reproduction may differ. (Raw data preserved.)"
                });
            }

            // ===== Faces missing =====
            bool hasAnyPolyFaces = HasAnyAttrKeyContains(mesh, ".fc[") || HasAnyAttrKeyContains(mesh, "fc[");
            if (!hasAnyPolyFaces && vertCount > 0)
            {
                outList.Add(new MeshLimitationRow
                {
                    MeshNodeName = mesh.Name,
                    IssueKey = "MeshFacesMissing_VerticesOnly",
                    Severity = "Warn",
                    Details =
                        "Vertices exist but polyFaces (fc[...]) not found. Tool will still create a Mesh as points/empty topology so the node is reconstructed; topology is preserved only as raw tokens if present elsewhere."
                });
            }
        }

        // -------------------------
        // Helpers: attribute scanning
        // -------------------------

        private static bool HasAnyAttrKeyContains(NodeRecord rec, string needle)
        {
            if (rec == null || rec.Attributes == null || string.IsNullOrEmpty(needle)) return false;
            foreach (var kv in rec.Attributes)
            {
                var k = kv.Key ?? "";
                if (k.IndexOf(needle, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        private static int EstimateIndexedArrayMaxPlusOne(NodeRecord rec, string[] prefixes)
        {
            if (rec == null || rec.Attributes == null || prefixes == null) return 0;

            int max = -1;
            foreach (var kv in rec.Attributes)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;

                bool match = false;
                for (int p = 0; p < prefixes.Length; p++)
                {
                    var pre = prefixes[p];
                    if (key.StartsWith(pre + "[", StringComparison.Ordinal) ||
                        key.StartsWith("." + pre + "[", StringComparison.Ordinal) ||
                        string.Equals(key, pre, StringComparison.Ordinal) ||
                        string.Equals(key, "." + pre, StringComparison.Ordinal))
                    {
                        match = true;
                        break;
                    }
                }
                if (!match) continue;

                if (TryParseIndexRange(key, out var start, out var end))
                    max = Math.Max(max, end);
            }

            return (max >= 0) ? (max + 1) : 0;
        }

        private static int FindMaxUvSetIndexInAttributes(NodeRecord rec)
        {
            if (rec == null || rec.Attributes == null) return -1;

            int max = -1;
            foreach (var kv in rec.Attributes)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;

                int uvst = key.IndexOf("uvst[", StringComparison.Ordinal);
                if (uvst < 0) uvst = key.IndexOf(".uvst[", StringComparison.Ordinal);
                if (uvst < 0) continue;

                int lb = key.IndexOf('[', uvst);
                int rb = key.IndexOf(']', uvst);
                if (lb < 0 || rb < 0 || rb <= lb + 1) continue;

                var inside = key.Substring(lb + 1, rb - lb - 1);
                if (int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    max = Math.Max(max, idx);
            }

            return max;
        }

        private static bool ScanPolyFacesForHoles(NodeRecord rec, out int maxFaceCornerCount, out int maxUvSetIndexInFaces, out bool hasFaceVaryingNormalTokens)
        {
            maxFaceCornerCount = 0;
            maxUvSetIndexInFaces = -1;
            hasFaceVaryingNormalTokens = false;

            if (rec == null || rec.Attributes == null) return false;

            bool hasHoles = false;

            foreach (var kv in rec.Attributes)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;
                if (!key.StartsWith(".fc", StringComparison.Ordinal) && !key.StartsWith("fc", StringComparison.Ordinal))
                    continue;

                var raw = kv.Value;
                if (raw == null || raw.ValueTokens == null || raw.ValueTokens.Count == 0) continue;

                var tokens = raw.ValueTokens;

                // scan tokens
                for (int i = 0; i < tokens.Count; i++)
                {
                    var t = tokens[i];
                    if (t == "h") hasHoles = true;
                    if (t == "mn" || t == "mf") hasFaceVaryingNormalTokens = true;

                    if (t == "f" && i + 1 < tokens.Count)
                    {
                        if (int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                            maxFaceCornerCount = Math.Max(maxFaceCornerCount, n);
                    }

                    if (t == "mu" && i + 2 < tokens.Count)
                    {
                        if (int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var setIdx))
                            maxUvSetIndexInFaces = Math.Max(maxUvSetIndexInFaces, setIdx);
                    }
                }
            }

            return hasHoles;
        }

        private static bool TryParseIndexRange(string key, out int start, out int end)
        {
            start = 0;
            end = -1;

            int lb = key.IndexOf('[');
            int rb = key.IndexOf(']');
            if (lb < 0 || rb < 0 || rb <= lb + 1)
                return false;

            var inside = key.Substring(lb + 1, rb - lb - 1);
            int colon = inside.IndexOf(':');
            if (colon >= 0)
            {
                var a = inside.Substring(0, colon);
                var b = inside.Substring(colon + 1);

                if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
                if (!int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out end)) return false;
                return true;
            }

            if (!int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
            end = start;
            return true;
        }
    }
}
