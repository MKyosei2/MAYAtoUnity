using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// .ma(ASCII) の mesh NodeRecord を「Maya無し」で復元するデコーダ。
    ///
    /// 目的:
    /// - Unity Mesh に収まる範囲は実メッシュ化
    /// - Unity に概念が無い/制限がある要素も、RawTokens/DecodedExtras として保持し「100%データ保持」を担保
    ///
    /// 対応(ベストエフォート):
    /// - vertices: vt/pt/pnts (float3)
    /// - polyFaces: fc[...] の token stream から f/mu/mn/ed/h を可能な範囲で解析
    /// - UV: uvst[i].uvsp
    /// - normals: n[...] + (mn/mf) による face-varying normal id (推定)
    /// - holes: h token は解析し、Unity側は「保存＋外周のみ三角化」(視覚差が出る可能性) を許容
    /// </summary>
    public static class MayaMaMeshDecoder
    {
        public sealed class Decoded
        {
            // Expanded stream (face-varying UV/Normals 対応)
            public Vector3[] Vertices;
            public Vector3[] Normals; // null可
            public List<Vector2[]> UvSets = new List<Vector2[]>(); // 0..7
            public Color[] Colors; // null可

            public int[] Triangles; // single-submesh triangles (submesh partitionは上位で)
            public int[] ExpandedToOriginalVertex;

            // Extras / audit
            public bool HasHoles;
            public int HoleFaceCount;
            public int UvSetCountDetected;
            public bool HasFaceVaryingNormals;
            public string Note;
        }

        public static bool TryDecode(NodeRecord meshShape, MayaImportLog log, out Decoded decoded, out string note)
        {
            decoded = null;
            note = null;

            if (meshShape == null || meshShape.Attributes == null)
            {
                note = "no NodeRecord/Attributes";
                return false;
            }

            // -------- vertices --------
            if (!TryCollectFloat3Array(meshShape, new[] { ".vt", "vt", ".pt", "pt", ".pnts", "pnts" }, out var vertsMaya, out var vNote))
            {
                note = "vertices missing (" + vNote + ")";
                return false;
            }

            // -------- normals pool (may be vertex-count OR separate pool) --------
            TryCollectFloat3Array(meshShape, new[] { ".n", "n" }, out var normalsPoolMaya, out _);

            // -------- colors (optional) --------
            TryCollectFloat4Array(meshShape, new[] { ".clr", "clr" }, out var colors, out _);

            // -------- UV pools (all uvst[*]) --------
            var uvPools = CollectUvPools(meshShape, out var uvSetMaxAttr);
            int uvSetCountDetected = (uvSetMaxAttr >= 0) ? (uvSetMaxAttr + 1) : uvPools.Count;

            // -------- faces --------
            if (!TryParsePolyFaces(meshShape, out var faces, out var maxUvSetIndexInFaces, out var anyHoles, out var holeFaceCount, out var anyFaceVaryingNormalIds))
            {
                // facesが無くても vertices を保持したいケースがあるので「成功」として返す(ポイントクラウド扱い)
                decoded = new Decoded
                {
                    Vertices = vertsMaya,
                    Triangles = Array.Empty<int>(),
                    ExpandedToOriginalVertex = BuildIdentityMap(vertsMaya.Length),
                    HasHoles = false,
                    HoleFaceCount = 0,
                    UvSetCountDetected = uvSetCountDetected,
                    HasFaceVaryingNormals = false,
                    Note = "No polyFaces; vertices only."
                };
                note = decoded.Note;
                return true;
            }

            int uvSetCount = Math.Max(uvPools.Count, maxUvSetIndexInFaces + 1);
            uvSetCount = Mathf.Clamp(uvSetCount, 0, 8); // Unity Mesh UV0..UV7

            // -------- expand vertices per (origV + uvIds[0..7] + normalId) --------
            var newVerts = new List<Vector3>(vertsMaya.Length);
            var newNormals = new List<Vector3>(vertsMaya.Length);
            var newColors = (colors != null && colors.Length == vertsMaya.Length) ? new List<Color>(vertsMaya.Length) : null;

            var newUvSets = new List<Vector2>[uvSetCount];
            for (int i = 0; i < uvSetCount; i++)
                newUvSets[i] = new List<Vector2>(vertsMaya.Length);

            var expandedToOrig = new List<int>(vertsMaya.Length);

            // Key
            var map = new Dictionary<VertKey, int>(VertKeyComparer.Instance);
            int[] uvIdsBuf = new int[Math.Max(uvSetCount, 1)];

            int GetOrCreate(int faceIndex, int cornerIndex, int origV, FaceData face)
            {
                FillUvIds(face, cornerIndex, uvIdsBuf, uvSetCount);

                int normalId = -1;
                if (anyFaceVaryingNormalIds && face != null && face.NormalIds != null && cornerIndex < face.NormalIds.Count)
                    normalId = face.NormalIds[cornerIndex];

                var key = new VertKey(origV, normalId,
                    uvSetCount > 0 ? uvIdsBuf[0] : -1,
                    uvSetCount > 1 ? uvIdsBuf[1] : -1,
                    uvSetCount > 2 ? uvIdsBuf[2] : -1,
                    uvSetCount > 3 ? uvIdsBuf[3] : -1,
                    uvSetCount > 4 ? uvIdsBuf[4] : -1,
                    uvSetCount > 5 ? uvIdsBuf[5] : -1,
                    uvSetCount > 6 ? uvIdsBuf[6] : -1,
                    uvSetCount > 7 ? uvIdsBuf[7] : -1);

                if (map.TryGetValue(key, out var idx))
                    return idx;

                idx = newVerts.Count;
                map[key] = idx;

                newVerts.Add(vertsMaya[origV]);
                expandedToOrig.Add(origV);

                // normals
                Vector3 nrm = Vector3.zero;
                if (normalsPoolMaya != null && normalsPoolMaya.Length > 0)
                {
                    if (normalId >= 0 && normalId < normalsPoolMaya.Length)
                        nrm = normalsPoolMaya[normalId];
                    else if (normalsPoolMaya.Length == vertsMaya.Length && origV >= 0 && origV < normalsPoolMaya.Length)
                        nrm = normalsPoolMaya[origV];
                }
                newNormals.Add(nrm);

                // colors
                if (newColors != null)
                    newColors.Add(colors[origV]);

                // UVs
                for (int si = 0; si < uvSetCount; si++)
                {
                    Vector2 uv = Vector2.zero;
                    if (uvPools.TryGetValue(si, out var pool) && pool != null)
                    {
                        int uvid = uvIdsBuf[si];
                        if (uvid >= 0 && uvid < pool.Length)
                            uv = pool[uvid];
                    }
                    newUvSets[si].Add(uv);
                }

                return idx;
            }

            var tris = new List<int>(faces.Count * 6);
            for (int fi = 0; fi < faces.Count; fi++)
            {
                var f = faces[fi];
                if (f == null || f.V == null || f.V.Count < 3)
                    continue;

                // NOTE: holesがある場合でも、ここは外周のみファン分割(holesはextrasとして保持)
                int n = f.V.Count;
                for (int i = 1; i + 1 < n; i++)
                {
                    int v0 = f.V[0];
                    int v1 = f.V[i];
                    int v2 = f.V[i + 1];

                    if (v0 < 0 || v0 >= vertsMaya.Length) continue;
                    if (v1 < 0 || v1 >= vertsMaya.Length) continue;
                    if (v2 < 0 || v2 >= vertsMaya.Length) continue;

                    int a = GetOrCreate(fi, 0, v0, f);
                    int b = GetOrCreate(fi, i, v1, f);
                    int c = GetOrCreate(fi, i + 1, v2, f);

                    tris.Add(a); tris.Add(b); tris.Add(c);
                }
            }

            decoded = new Decoded
            {
                Vertices = newVerts.ToArray(),
                Normals = (newNormals.Count == newVerts.Count) ? newNormals.ToArray() : null,
                Colors = (newColors != null && newColors.Count == newVerts.Count) ? newColors.ToArray() : null,
                Triangles = tris.ToArray(),
                ExpandedToOriginalVertex = expandedToOrig.ToArray(),
                HasHoles = anyHoles,
                HoleFaceCount = holeFaceCount,
                UvSetCountDetected = uvSetCountDetected,
                HasFaceVaryingNormals = anyFaceVaryingNormalIds,
            };

            decoded.UvSets.Clear();
            for (int i = 0; i < uvSetCount; i++)
                decoded.UvSets.Add(newUvSets[i].ToArray());

            decoded.Note =
                $"decoded expanded vtx={decoded.Vertices.Length}, tris={decoded.Triangles.Length / 3}, uvSets={uvSetCount}, " +
                $"holes={(decoded.HasHoles ? ("yes(" + decoded.HoleFaceCount + ")") : "no")}, " +
                $"fvNormals={(decoded.HasFaceVaryingNormals ? "yes" : "no")}";

            note = decoded.Note;
            return true;
        }

        // =========================
        // PolyFaces parsing
        // =========================

        private sealed class FaceData
        {
            public List<int> V;
            public Dictionary<int, List<int>> UvIdsPerSet;
            public List<int> NormalIds; // face-varying normal ids (best-effort)
            public bool HasHoles;
        }

        private static bool TryParsePolyFaces(
            NodeRecord rec,
            out List<FaceData> faces,
            out int maxUvSetIndex,
            out bool anyHoles,
            out int holeFaceCount,
            out bool anyFaceVaryingNormalIds)
        {
            faces = null;
            maxUvSetIndex = -1;
            anyHoles = false;
            holeFaceCount = 0;
            anyFaceVaryingNormalIds = false;

            var chunks = new List<(int start, int end, List<string> tokens)>();

            foreach (var kv in rec.Attributes)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;
                if (!key.StartsWith(".fc", StringComparison.Ordinal) && !key.StartsWith("fc", StringComparison.Ordinal))
                    continue;

                if (!TryParseRange(key, out var s, out var e))
                    continue;

                var tokens = CleanValueTokens(kv.Value);
                if (tokens.Count == 0) continue;

                chunks.Add((s, e, tokens));
            }

            if (chunks.Count == 0) return false;

            int maxIndex = -1;
            for (int i = 0; i < chunks.Count; i++)
                maxIndex = Math.Max(maxIndex, chunks[i].end);

            if (maxIndex < 0) return false;

            var arr = new FaceData[maxIndex + 1];

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var (start, end, tokens) = chunks[ci];
                int t = 0;

                for (int fi = start; fi <= end; fi++)
                {
                    var face = new FaceData();

                    // face header: f <n> <v...>
                    if (t < tokens.Count && tokens[t] == "f")
                    {
                        t++;
                        if (t >= tokens.Count) break;
                        if (!TryI(tokens[t++], out var n)) break;

                        face.V = new List<int>(Mathf.Max(3, n));
                        for (int k = 0; k < n && t < tokens.Count; k++)
                        {
                            if (!TryI(tokens[t++], out var v)) break;
                            face.V.Add(v);
                        }
                    }

                    // tags until next 'f'
                    while (t < tokens.Count)
                    {
                        var tag = tokens[t];
                        if (tag == "f") break;
                        t++;

                        if (tag == "mu")
                        {
                            // mu <setIndex> <n> <uvid...>
                            if (t + 1 >= tokens.Count) break;
                            if (!TryI(tokens[t++], out var setIdx)) break;
                            if (!TryI(tokens[t++], out var n)) break;

                            if (face.UvIdsPerSet == null)
                                face.UvIdsPerSet = new Dictionary<int, List<int>>();

                            var list = new List<int>(Mathf.Max(3, n));
                            for (int k = 0; k < n && t < tokens.Count; k++)
                            {
                                if (!TryI(tokens[t++], out var uvid)) break;
                                list.Add(uvid);
                            }

                            face.UvIdsPerSet[setIdx] = list;
                            if (setIdx > maxUvSetIndex) maxUvSetIndex = setIdx;
                        }
                        else if (tag == "mn" || tag == "mf")
                        {
                            // best-effort: <n> <normalId...>
                            if (t >= tokens.Count) break;
                            if (!TryI(tokens[t++], out var n)) break;

                            face.NormalIds ??= new List<int>(Mathf.Max(3, n));
                            for (int k = 0; k < n && t < tokens.Count; k++)
                            {
                                if (!TryI(tokens[t++], out var nid)) break;
                                face.NormalIds.Add(nid);
                            }

                            if (face.NormalIds.Count > 0)
                                anyFaceVaryingNormalIds = true;
                        }
                        else if (tag == "h")
                        {
                            // holes: best-effort parse to avoid breaking stream
                            // Common patterns vary. We mark HasHoles and try to skip ints safely.
                            face.HasHoles = true;
                            anyHoles = true;
                            holeFaceCount++;

                            // Attempt: h <holeCount> then for each hole: <m> <v...>
                            if (t < tokens.Count && TryI(tokens[t], out var holeCount))
                            {
                                t++;
                                for (int hi = 0; hi < holeCount && t < tokens.Count; hi++)
                                {
                                    if (!TryI(tokens[t++], out var m)) break;
                                    for (int k = 0; k < m && t < tokens.Count; k++)
                                    {
                                        // consume vertex ids
                                        if (!TryI(tokens[t++], out _)) break;
                                    }
                                }
                            }
                        }
                        else if (tag == "ed")
                        {
                            // ed <n> <edgeRef...>
                            if (t >= tokens.Count) break;
                            if (!TryI(tokens[t++], out var n)) break;
                            for (int k = 0; k < n && t < tokens.Count; k++)
                            {
                                // consume
                                if (!TryI(tokens[t++], out _)) break;
                            }
                        }
                        else
                        {
                            // Unknown tag: best-effort skip if next is a count.
                            if (t < tokens.Count && TryI(tokens[t], out var n))
                            {
                                t++;
                                for (int k = 0; k < n && t < tokens.Count; k++)
                                {
                                    // consume
                                    t++;
                                }
                            }
                            else
                            {
                                // cannot safely skip; stop this face tag parsing
                                break;
                            }
                        }
                    }

                    arr[fi] = face;
                }
            }

            faces = new List<FaceData>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
                faces.Add(arr[i]);

            return true;
        }

        private static void FillUvIds(FaceData f, int cornerIndex, int[] buffer, int uvSetCount)
        {
            for (int i = 0; i < uvSetCount; i++) buffer[i] = -1;
            if (f == null || f.UvIdsPerSet == null) return;

            for (int si = 0; si < uvSetCount; si++)
            {
                if (!f.UvIdsPerSet.TryGetValue(si, out var list) || list == null || list.Count == 0)
                    continue;

                int idx = Mathf.Clamp(cornerIndex, 0, list.Count - 1);
                buffer[si] = list[idx];
            }
        }

        // =========================
        // Collectors
        // =========================

        private static Dictionary<int, Vector2[]> CollectUvPools(NodeRecord rec, out int maxSetIndex)
        {
            maxSetIndex = -1;
            var pools = new Dictionary<int, Vector2[]>();

            // group keys by set index
            var chunksBySet = new Dictionary<int, List<(int start, int end, List<string> tokens)>>();

            foreach (var kv in rec.Attributes)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;

                int uvst = key.IndexOf("uvst[", StringComparison.Ordinal);
                if (uvst < 0) uvst = key.IndexOf(".uvst[", StringComparison.Ordinal);
                if (uvst < 0) continue;

                int rb = key.IndexOf(']', uvst);
                if (rb < 0) continue;

                var setIdxStr = key.Substring(uvst + "uvst[".Length, rb - (uvst + "uvst[".Length));
                if (!TryI(setIdxStr, out var setIndex)) continue;

                int uvsp = key.IndexOf("uvsp[", StringComparison.Ordinal);
                if (uvsp < 0) uvsp = key.IndexOf(".uvsp[", StringComparison.Ordinal);
                if (uvsp < 0) continue;

                if (!TryParseRange(key.Substring(uvsp), out var start, out var end)) continue;

                var tokens = CleanValueTokens(kv.Value);
                if (tokens.Count < 2) continue;

                if (!chunksBySet.TryGetValue(setIndex, out var list))
                {
                    list = new List<(int start, int end, List<string> tokens)>();
                    chunksBySet[setIndex] = list;
                }

                list.Add((start, end, tokens));
                if (setIndex > maxSetIndex) maxSetIndex = setIndex;
            }

            foreach (var kv in chunksBySet)
            {
                int setIndex = kv.Key;
                var chunks = kv.Value;
                if (chunks == null || chunks.Count == 0) continue;

                int maxIndex = -1;
                for (int i = 0; i < chunks.Count; i++)
                    maxIndex = Math.Max(maxIndex, chunks[i].end);

                if (maxIndex < 0) continue;

                var arr = new Vector2[maxIndex + 1];

                for (int ci = 0; ci < chunks.Count; ci++)
                {
                    var (start, end, tokens) = chunks[ci];

                    int expected = (end - start + 1) * 2;
                    if (tokens.Count < expected) continue;

                    int t = 0;
                    for (int uvi = start; uvi <= end; uvi++)
                    {
                        if (!TryF(tokens[t++], out var u)) break;
                        if (!TryF(tokens[t++], out var v)) break;

                        if (uvi < 0 || uvi >= arr.Length) continue;
                        arr[uvi] = new Vector2(u, v);
                    }
                }

                pools[setIndex] = arr;
            }

            return pools;
        }

        private static bool TryCollectFloat3Array(NodeRecord rec, string[] prefixes, out Vector3[] arr, out string note)
        {
            arr = null;
            note = null;

            var map = new SortedDictionary<int, Vector3>();
            bool any = false;

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

                any = true;

                if (!TryParseRange(key, out var start, out var end))
                {
                    start = 0;
                    end = -1;
                }

                var tokens = CleanValueTokens(kv.Value);
                if (tokens.Count < 3) continue;

                int idx = 0;
                int count = (end >= start) ? (end - start + 1) : (tokens.Count / 3);

                for (int i = 0; i < count; i++)
                {
                    if (idx + 2 >= tokens.Count) break;

                    if (TryF(tokens[idx++], out var x) &&
                        TryF(tokens[idx++], out var y) &&
                        TryF(tokens[idx++], out var z))
                    {
                        map[start + i] = new Vector3(x, y, z);
                    }
                    else
                    {
                        idx = Math.Min(idx + 3, tokens.Count);
                    }
                }
            }

            if (!any)
            {
                note = "no matching attributes";
                return false;
            }
            if (map.Count == 0)
            {
                note = "no parsed float3 values";
                return false;
            }

            int max = -1;
            foreach (var k in map.Keys) max = k;

            var outArr = new Vector3[max + 1];
            foreach (var kv in map)
                outArr[kv.Key] = kv.Value;

            arr = outArr;
            note = $"segments={map.Count}, maxIndex={max}";
            return true;
        }

        private static bool TryCollectFloat4Array(NodeRecord rec, string[] prefixes, out Color[] arr, out string note)
        {
            arr = null;
            note = null;

            var map = new SortedDictionary<int, Color>();
            bool any = false;

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

                any = true;

                if (!TryParseRange(key, out var start, out var end))
                {
                    start = 0;
                    end = -1;
                }

                var tokens = CleanValueTokens(kv.Value);
                if (tokens.Count < 4) continue;

                int idx = 0;
                int count = (end >= start) ? (end - start + 1) : (tokens.Count / 4);

                for (int i = 0; i < count; i++)
                {
                    if (idx + 3 >= tokens.Count) break;

                    if (TryF(tokens[idx++], out var r) &&
                        TryF(tokens[idx++], out var g) &&
                        TryF(tokens[idx++], out var b) &&
                        TryF(tokens[idx++], out var a))
                    {
                        map[start + i] = new Color(r, g, b, a);
                    }
                    else
                    {
                        idx = Math.Min(idx + 4, tokens.Count);
                    }
                }
            }

            if (!any || map.Count == 0)
            {
                note = "no color array";
                return false;
            }

            int max = -1;
            foreach (var k in map.Keys) max = k;

            var outArr = new Color[max + 1];
            foreach (var kv in map)
                outArr[kv.Key] = kv.Value;

            arr = outArr;
            note = $"colors={arr.Length}";
            return true;
        }

        private static List<string> CleanValueTokens(RawAttributeValue raw)
        {
            if (raw == null || raw.ValueTokens == null) return new List<string>(0);

            // remove "setAttr -type ..." 由来の quote 等をここでは扱わず、数値のTokenだけ使う
            var t = raw.ValueTokens;
            if (t.Count == 0) return new List<string>(0);

            var list = new List<string>(t.Count);
            for (int i = 0; i < t.Count; i++)
            {
                var s = t[i];
                if (string.IsNullOrEmpty(s)) continue;
                list.Add(s);
            }
            return list;
        }

        private static bool TryParseRange(string key, out int start, out int end)
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
                if (!TryI(a, out start)) return false;
                if (!TryI(b, out end)) return false;
                return true;
            }

            if (!TryI(inside, out start)) return false;
            end = start;
            return true;
        }

        private static bool TryI(string s, out int v)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

        private static bool TryF(string s, out float v)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        private static int[] BuildIdentityMap(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            return a;
        }

        // =========================
        // Expanded vertex key
        // =========================

        private readonly struct VertKey
        {
            public readonly int V;
            public readonly int NormalId;
            public readonly int UV0, UV1, UV2, UV3, UV4, UV5, UV6, UV7;

            public VertKey(int v, int normalId, int uv0, int uv1, int uv2, int uv3, int uv4, int uv5, int uv6, int uv7)
            {
                V = v;
                NormalId = normalId;
                UV0 = uv0; UV1 = uv1; UV2 = uv2; UV3 = uv3;
                UV4 = uv4; UV5 = uv5; UV6 = uv6; UV7 = uv7;
            }
        }

        private sealed class VertKeyComparer : IEqualityComparer<VertKey>
        {
            public static readonly VertKeyComparer Instance = new VertKeyComparer();

            public bool Equals(VertKey a, VertKey b)
            {
                return a.V == b.V && a.NormalId == b.NormalId &&
                       a.UV0 == b.UV0 && a.UV1 == b.UV1 && a.UV2 == b.UV2 && a.UV3 == b.UV3 &&
                       a.UV4 == b.UV4 && a.UV5 == b.UV5 && a.UV6 == b.UV6 && a.UV7 == b.UV7;
            }

            public int GetHashCode(VertKey k)
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + k.V;
                    h = h * 31 + k.NormalId;
                    h = h * 31 + k.UV0; h = h * 31 + k.UV1; h = h * 31 + k.UV2; h = h * 31 + k.UV3;
                    h = h * 31 + k.UV4; h = h * 31 + k.UV5; h = h * 31 + k.UV6; h = h * 31 + k.UV7;
                    return h;
                }
            }
        }
    }
}
