// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 mesh payload locator for .mb:
    /// ŁiWindow Searchj
    ///
    /// ړI:
    /// - .mb  chunk decode Amesh(shape)m[hɑ΂Čpayloadifloat/uintj̎QƂt^
    /// - "Oochunk̎ӁiOEBhEj" TďER炷
    /// - t^Additivê݁iRawBinaryBytes͏ɕێj
    ///
    /// t^Q:
    /// - Float chunk refs: up to 10 (positions/normals/uv/uv2 candidates)
    /// - UInt  chunk refs: up to 8  (indices/polyFaces candidates)
    /// </summary>
    public static class MayaMbMeshChunkLocator
    {
        private const string KeyMeshChunkNote = ".mbMeshChunkNote";
        private const string KeyMeshChunkAnchor = ".mbMeshChunkAnchor";

        private const int MaxFloatRefs = 10;
        private const int MaxUIntRefs = 8;

        // AnchoȓOTEBhE
        private const int WindowBefore = 64;
        private const int WindowAfter = 512;

        // Deptheianchor depth ɁAǂ̒x̐[chunk܂Œǂj
        private const int DepthDownTolerance = 2;  // anchorDepth - 2 ܂ŋe
        private const int DepthUpTolerance = 8;    // anchorDepth + 8 ܂ŋe

        private sealed class Anchor
        {
            public string MeshKey;
            public int FirstIndex;
            public int BestIndex;
            public int BestDepth;
            public int HitCount;
        }

        private struct Candidate
        {
            public MayaBinaryIndex.ChunkInfo Chunk;
            public float Score;
        }

        public static void Apply(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null) return;
            if (scene.SourceKind != MayaSourceKind.BinaryMb) return;
            if (scene.MbIndex == null || scene.MbIndex.Chunks == null) return;
            if (scene.Nodes == null || scene.Nodes.Count == 0) return;

            var chunks = scene.MbIndex.Chunks;
            if (chunks.Count == 0) return;

            // ---------------------------------------------------------
            // 1) meshm[hW߂iNodeType=mesh + Shapej
            // ---------------------------------------------------------
            var meshKeys = new HashSet<string>(StringComparer.Ordinal);
            var leafToKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var kv in scene.Nodes)
            {
                var rec = kv.Value;
                if (rec == null || string.IsNullOrEmpty(rec.Name)) continue;

                var leaf = LeafOfDag(rec.Name);
                if (!string.IsNullOrEmpty(rec.NodeType) && rec.NodeType == "mesh")
                {
                    meshKeys.Add(rec.Name);
                    AddLeafMap(leafToKeys, leaf, rec.Name);
                    continue;
                }

                // meshshapeEibest-effortj
                if (!string.IsNullOrEmpty(leaf) && leaf.EndsWith("Shape", StringComparison.Ordinal))
                {
                    meshKeys.Add(rec.Name);
                    AddLeafMap(leafToKeys, leaf, rec.Name);

                    // unknown/transform/empty  mesh Ɋ񂹂iibuilder₷j
                    if (rec.NodeType == "unknown" || rec.NodeType == "transform" || string.IsNullOrEmpty(rec.NodeType))
                        rec.NodeType = "mesh";
                }
            }

            if (meshKeys.Count == 0)
            {
                log?.Info(".mb mesh-locate: no mesh/shape nodes found. (OK)");
                return;
            }

            // ---------------------------------------------------------
            // 2) chunk1񑖍 anchor W߂
            //    - decodedStrings  DAGpX or leaf o anchor ƂċL^
            // ---------------------------------------------------------
            var anchors = new Dictionary<string, Anchor>(StringComparer.Ordinal);

            for (int i = 0; i < chunks.Count; i++)
            {
                var ch = chunks[i];
                if (ch == null) continue;
                if (ch.DecodedStrings == null || ch.DecodedStrings.Length == 0) continue;

                // chunkQƂĂ\̂meshKeyQ
                var referenced = GetReferencedMeshKeys(meshKeys, leafToKeys, ch.DecodedStrings);

                if (referenced.Count == 0) continue;

                for (int r = 0; r < referenced.Count; r++)
                {
                    var meshKey = referenced[r];
                    if (string.IsNullOrEmpty(meshKey)) continue;

                    if (!anchors.TryGetValue(meshKey, out var a))
                    {
                        a = new Anchor
                        {
                            MeshKey = meshKey,
                            FirstIndex = i,
                            BestIndex = i,
                            BestDepth = ch.Depth,
                            HitCount = 1
                        };
                        anchors.Add(meshKey, a);
                    }
                    else
                    {
                        a.HitCount++;

                        // BestIndex ́uhitchunkv̗piɊق payload ɋ߂P[Xj
                        // depth͍Ōɍ̗p
                        a.BestIndex = i;
                        a.BestDepth = ch.Depth;
                    }
                }
            }

            // anchorȂ mesh ́AWbN gShapeh EiŌ̕یj
            if (anchors.Count == 0)
            {
                // fallback: "Shape" leaf L[ɁAExtractedStrings T
                var fallbackAnchors = BuildFallbackAnchorsFromExtractedStrings(scene, meshKeys, leafToKeys);
                foreach (var kv in fallbackAnchors)
                    anchors[kv.Key] = kv.Value;
            }

            // ---------------------------------------------------------
            // 3) anchor windowTČchunk reft^
            // ---------------------------------------------------------
            int floatAddedTotal = 0;
            int uintAddedTotal = 0;
            int meshTouched = 0;

            foreach (var kv in anchors)
            {
                var meshKey = kv.Key;
                var a = kv.Value;

                if (!scene.Nodes.TryGetValue(meshKey, out var meshRec) || meshRec == null)
                    continue;

                int before = WindowBefore;
                int after = WindowAfter;

                int anchorIndex = Clamp(a.BestIndex, 0, chunks.Count - 1);
                int anchorDepth = a.BestDepth;

                int start = Clamp(anchorIndex - before, 0, chunks.Count - 1);
                int end = Clamp(anchorIndex + after, 0, chunks.Count - 1);

                var floatCandidates = new List<Candidate>(64);
                var uintCandidates = new List<Candidate>(64);

                for (int i = start; i <= end; i++)
                {
                    var ch = chunks[i];
                    if (ch == null) continue;
                    if (ch.IsContainer) continue;

                    // depth gate
                    if (anchorDepth >= 0)
                    {
                        if (ch.Depth < anchorDepth - DepthDownTolerance) continue;
                        if (ch.Depth > anchorDepth + DepthUpTolerance) continue;
                    }

                    if (ch.DecodedKind == MayaBinaryDecoders.Kind.Float32BE)
                    {
                        if (IsPlausibleFloatPayload(ch))
                        {
                            floatCandidates.Add(new Candidate { Chunk = ch, Score = ScoreFloatCandidate(ch, anchorIndex, i, anchorDepth) });
                        }
                        continue;
                    }

                    if (ch.DecodedKind == MayaBinaryDecoders.Kind.UInt32BE)
                    {
                        if (IsPlausibleUIntPayload(ch))
                        {
                            uintCandidates.Add(new Candidate { Chunk = ch, Score = ScoreUIntCandidate(ch, anchorIndex, i, anchorDepth) });
                        }
                    }
                }

                // sort by score desc
                floatCandidates.Sort((x, y) => y.Score.CompareTo(x.Score));
                uintCandidates.Sort((x, y) => y.Score.CompareTo(x.Score));

                // attach refs (avoid duplicates)
                int fAdded = AttachBestFloatRefs(meshRec, floatCandidates);
                int uAdded = AttachBestUIntRefs(meshRec, uintCandidates);

                if (fAdded > 0 || uAdded > 0)
                {
                    meshTouched++;
                    floatAddedTotal += fAdded;
                    uintAddedTotal += uAdded;

                    EnsureNote(meshRec);
                    EnsureAnchor(meshRec, a, anchorIndex, anchorDepth, start, end);
                }
            }

            log?.Info($".mb mesh-locate(window): meshNodes={meshKeys.Count}, anchors={anchors.Count}, touched={meshTouched}, floatRefsAdded={floatAddedTotal}, uintRefsAdded={uintAddedTotal}.");
        }

        // =========================================================
        // Attach
        // =========================================================

        private static int AttachBestFloatRefs(NodeRecord rec, List<Candidate> candidates)
        {
            if (rec == null) return 0;

            var used = BuildExistingRefSet(rec, isFloat: true);
            int added = 0;

            for (int i = 0; i < candidates.Count && added < MaxFloatRefs; i++)
            {
                var ch = candidates[i].Chunk;
                string refStr = MakeChunkRef(ch);

                if (used.Contains(refStr)) continue;
                if (TryAddFloatRef(rec, ch))
                {
                    used.Add(refStr);
                    added++;
                }
            }

            return added;
        }

        private static int AttachBestUIntRefs(NodeRecord rec, List<Candidate> candidates)
        {
            if (rec == null) return 0;

            var used = BuildExistingRefSet(rec, isFloat: false);
            int added = 0;

            for (int i = 0; i < candidates.Count && added < MaxUIntRefs; i++)
            {
                var ch = candidates[i].Chunk;
                string refStr = MakeChunkRef(ch);

                if (used.Contains(refStr)) continue;
                if (TryAddUIntRef(rec, ch))
                {
                    used.Add(refStr);
                    added++;
                }
            }

            return added;
        }

        private static HashSet<string> BuildExistingRefSet(NodeRecord rec, bool isFloat)
        {
            var hs = new HashSet<string>(StringComparer.Ordinal);

            if (rec == null) return hs;

            int max = isFloat ? MaxFloatRefs : MaxUIntRefs;
            for (int i = 1; i <= max; i++)
            {
                string key = isFloat ? FloatKey(i) : UIntKey(i);
                if (rec.Attributes.TryGetValue(key, out var v) && v != null && v.ValueTokens != null && v.ValueTokens.Count > 0)
                {
                    hs.Add(v.ValueTokens[0]);
                }
            }

            return hs;
        }

        private static void EnsureNote(NodeRecord rec)
        {
            if (rec == null) return;

            if (!rec.Attributes.ContainsKey(KeyMeshChunkNote))
            {
                rec.Attributes[KeyMeshChunkNote] = new RawAttributeValue(
                    "string",
                    new List<string>
                    {
                        "Chunk refs attached (window-search). Decoder will choose pos/nrm/uv and indices/polyFaces best-effort."
                    }
                );
            }
        }

        private static void EnsureAnchor(NodeRecord rec, Anchor a, int anchorIndex, int anchorDepth, int start, int end)
        {
            if (rec == null || a == null) return;

            rec.Attributes[KeyMeshChunkAnchor] = new RawAttributeValue(
                "string",
                new List<string>
                {
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "anchorIndex={0};anchorDepth={1};hits={2};window=[{3}..{4}]",
                        anchorIndex, anchorDepth, a.HitCount, start, end
                    )
                }
            );
        }

        private static bool TryAddFloatRef(NodeRecord rec, MayaBinaryIndex.ChunkInfo ch)
        {
            for (int i = 1; i <= MaxFloatRefs; i++)
            {
                var key = FloatKey(i);
                if (!rec.Attributes.ContainsKey(key))
                {
                    rec.Attributes[key] = new RawAttributeValue("string", new List<string> { MakeChunkRef(ch) });
                    return true;
                }
            }
            return false;
        }

        private static bool TryAddUIntRef(NodeRecord rec, MayaBinaryIndex.ChunkInfo ch)
        {
            for (int i = 1; i <= MaxUIntRefs; i++)
            {
                var key = UIntKey(i);
                if (!rec.Attributes.ContainsKey(key))
                {
                    rec.Attributes[key] = new RawAttributeValue("string", new List<string> { MakeChunkRef(ch) });
                    return true;
                }
            }
            return false;
        }

        private static string FloatKey(int idx) => idx == 1 ? ".mbMeshFloatChunkRef" : $".mbMeshFloatChunkRef{idx}";
        private static string UIntKey(int idx) => idx == 1 ? ".mbMeshUIntChunkRef" : $".mbMeshUIntChunkRef{idx}";

        // =========================================================
        // Candidate gating + scoring
        // =========================================================

        private static bool IsPlausibleFloatPayload(MayaBinaryIndex.ChunkInfo ch)
        {
            if (ch == null) return false;
            if (ch.DataSize < 36) return false;     // at least 9 floats => 3 vertices
            if ((ch.DataSize % 4) != 0) return false;

            // if we have a decoded sample, prefer those with finite values
            var f = ch.DecodedFloats;
            if (f != null && f.Length > 0)
            {
                int finite = 0;
                for (int i = 0; i < f.Length; i++)
                {
                    var v = f[i];
                    if (!float.IsNaN(v) && !float.IsInfinity(v)) finite++;
                }
                if (finite <= 0) return false;
            }

            return true;
        }

        private static bool IsPlausibleUIntPayload(MayaBinaryIndex.ChunkInfo ch)
        {
            if (ch == null) return false;
            if (ch.DataSize < 12) return false;     // at least 3 uints
            if ((ch.DataSize % 4) != 0) return false;
            return true;
        }

        private static float ScoreFloatCandidate(MayaBinaryIndex.ChunkInfo ch, int anchorIndex, int idx, int anchorDepth)
        {
            // Base: larger payload tends to be mesh buffers
            float sizeScore = (float)Math.Log(1.0 + ch.DataSize) * 2.0f;

            // Distance: closer to anchor is better
            int dist = Math.Abs(idx - anchorIndex);
            float distScore = -dist * 0.01f;

            // Depth closeness: closer is better
            float depthScore = 0f;
            if (anchorDepth >= 0)
            {
                int dd = Math.Abs(ch.Depth - anchorDepth);
                depthScore = -dd * 0.15f;
            }

            // Sample heuristic:
            // - Positions: wider range, some negatives
            // - Normals: mostly [-1..1]
            // - UV: mostly [0..1]
            float sampleScore = 0f;
            var f = ch.DecodedFloats;
            if (f != null && f.Length > 0)
            {
                sampleScore += GuessPositionLikeScore(f) * 1.0f;
                sampleScore += GuessNormalLikeScore(f) * 0.5f;
                sampleScore += GuessUvLikeScore(f) * 0.5f;
            }

            return sizeScore + distScore + depthScore + sampleScore;
        }

        private static float ScoreUIntCandidate(MayaBinaryIndex.ChunkInfo ch, int anchorIndex, int idx, int anchorDepth)
        {
            float sizeScore = (float)Math.Log(1.0 + ch.DataSize) * 2.0f;

            int dist = Math.Abs(idx - anchorIndex);
            float distScore = -dist * 0.01f;

            float depthScore = 0f;
            if (anchorDepth >= 0)
            {
                int dd = Math.Abs(ch.Depth - anchorDepth);
                depthScore = -dd * 0.15f;
            }

            // Sample heuristic:
            // - polyFaceŝ悤ɕꍇiintƂēǂނƕɂȂjƂď_
            float sampleScore = 0f;
            var u = ch.DecodedUInts;
            if (u != null && u.Length > 0)
            {
                // u  uint Ȃ̂ŕ͂łȂA0/1΂⋐l΂
                int small = 0;
                int huge = 0;
                for (int i = 0; i < u.Length; i++)
                {
                    if (u[i] <= 3) small++;
                    if (u[i] > 10000000u) huge++;
                }
                sampleScore -= small * 0.05f;
                sampleScore -= huge * 0.1f;
            }

            return sizeScore + distScore + depthScore + sampleScore;
        }

        private static float GuessPositionLikeScore(float[] f)
        {
            // wider range & some negatives is better
            if (f == null || f.Length == 0) return 0f;

            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            int neg = 0;
            int in01 = 0;

            int n = Math.Min(f.Length, 16);
            for (int i = 0; i < n; i++)
            {
                float v = f[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                if (v < min) min = v;
                if (v > max) max = v;
                if (v < 0f) neg++;
                if (v >= 0f && v <= 1f) in01++;
            }

            float range = max - min;
            return (float)Math.Log(1.0 + Math.Abs(range)) * 1.0f + neg * 0.15f - in01 * 0.1f;
        }

        private static float GuessNormalLikeScore(float[] f)
        {
            // values mostly within [-1..1]
            if (f == null || f.Length == 0) return 0f;

            int n = Math.Min(f.Length, 16);
            int inRange = 0;

            for (int i = 0; i < n; i++)
            {
                float v = f[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                if (v >= -1.2f && v <= 1.2f) inRange++;
            }

            return inRange * 0.08f;
        }

        private static float GuessUvLikeScore(float[] f)
        {
            // values mostly in [0..1]
            if (f == null || f.Length == 0) return 0f;

            int n = Math.Min(f.Length, 16);
            int inRange = 0;

            for (int i = 0; i < n; i++)
            {
                float v = f[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                if (v >= -0.25f && v <= 1.25f) inRange++;
            }

            return inRange * 0.05f;
        }

        // =========================================================
        // Anchor detection helpers
        // =========================================================

        private static List<string> GetReferencedMeshKeys(HashSet<string> meshKeys, Dictionary<string, List<string>> leafToKeys, string[] decodedStrings)
        {
            var outKeys = new List<string>(4);

            for (int i = 0; i < decodedStrings.Length; i++)
            {
                var s = decodedStrings[i];
                if (string.IsNullOrEmpty(s)) continue;

                // DAG path direct match
                if (LooksLikeDagPath(s))
                {
                    var norm = NormalizeDag(s);
                    if (meshKeys.Contains(norm))
                    {
                        outKeys.Add(norm);
                        continue;
                    }

                    // leaf match
                    var leaf = LeafOfDag(norm);
                    if (!string.IsNullOrEmpty(leaf) && leafToKeys.TryGetValue(leaf, out var list))
                    {
                        for (int k = 0; k < list.Count; k++)
                            outKeys.Add(list[k]);
                    }

                    continue;
                }

                // leaf direct match
                if (leafToKeys.TryGetValue(s, out var keys))
                {
                    for (int k = 0; k < keys.Count; k++)
                        outKeys.Add(keys[k]);
                    continue;
                }

                // common shape suffix
                if (s.EndsWith("Shape", StringComparison.Ordinal) && leafToKeys.TryGetValue(s, out keys))
                {
                    for (int k = 0; k < keys.Count; k++)
                        outKeys.Add(keys[k]);
                }
            }

            // dedupe
            if (outKeys.Count > 1)
            {
                var hs = new HashSet<string>(StringComparer.Ordinal);
                var dedup = new List<string>(outKeys.Count);
                for (int i = 0; i < outKeys.Count; i++)
                {
                    if (hs.Add(outKeys[i]))
                        dedup.Add(outKeys[i]);
                }
                return dedup;
            }

            return outKeys;
        }

        private static Dictionary<string, Anchor> BuildFallbackAnchorsFromExtractedStrings(
            MayaSceneData scene,
            HashSet<string> meshKeys,
            Dictionary<string, List<string>> leafToKeys)
        {
            var anchors = new Dictionary<string, Anchor>(StringComparer.Ordinal);
            if (scene?.MbIndex?.ExtractedStrings == null) return anchors;

            // ExtractedStringsɂ͑ʂ̕񂪓Ă邱Ƃ邪Aanchor "ŏɌ" xŏ\
            for (int si = 0; si < scene.MbIndex.ExtractedStrings.Count; si++)
            {
                var s = scene.MbIndex.ExtractedStrings[si];
                if (string.IsNullOrEmpty(s)) continue;

                if (LooksLikeDagPath(s))
                {
                    var norm = NormalizeDag(s);
                    if (meshKeys.Contains(norm))
                    {
                        if (!anchors.ContainsKey(norm))
                        {
                            anchors[norm] = new Anchor { MeshKey = norm, FirstIndex = 0, BestIndex = 0, BestDepth = 0, HitCount = 1 };
                        }
                    }
                    continue;
                }

                if (leafToKeys.TryGetValue(s, out var list))
                {
                    for (int k = 0; k < list.Count; k++)
                    {
                        var mk = list[k];
                        if (!anchors.ContainsKey(mk))
                            anchors[mk] = new Anchor { MeshKey = mk, FirstIndex = 0, BestIndex = 0, BestDepth = 0, HitCount = 1 };
                    }
                }
            }

            return anchors;
        }

        // =========================================================
        // Chunk ref formatting
        // =========================================================

        private static string MakeChunkRef(MayaBinaryIndex.ChunkInfo ch)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "TAG={0};OFF={1};DATA={2};SIZE={3};KIND={4};DEPTH={5}",
                ch.Id, ch.Offset, ch.DataOffset, ch.DataSize, ch.DecodedKind, ch.Depth
            );
        }

        // =========================================================
        // Small utilities
        // =========================================================

        private static void AddLeafMap(Dictionary<string, List<string>> map, string leaf, string fullKey)
        {
            if (string.IsNullOrEmpty(leaf) || string.IsNullOrEmpty(fullKey)) return;

            if (!map.TryGetValue(leaf, out var list))
            {
                list = new List<string>(1);
                map.Add(leaf, list);
            }

            // avoid duplicates
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], fullKey, StringComparison.Ordinal))
                    return;

            list.Add(fullKey);
        }

        private static bool LooksLikeDagPath(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length < 3 || s.Length > 200) return false;
            return s[0] == '|' && s.IndexOf('|', 1) > 0;
        }

        private static string NormalizeDag(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s[0] != '|') s = "|" + s;
            while (s.Length > 1 && s[s.Length - 1] == '|')
                s = s.Substring(0, s.Length - 1);
            return s;
        }

        private static string LeafOfDag(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int last = name.LastIndexOf('|');
            if (last >= 0 && last < name.Length - 1) return name.Substring(last + 1);
            return name;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
