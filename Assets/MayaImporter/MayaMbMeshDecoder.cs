// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace MayaImporter.Core
{
    /// <summary>
    /// .mb(Binary) -> Unity Mesh best-effort decoder.
    /// Phase-4 (compile hotfix):
    /// - Fully consistent compilation (no dangling uvPicked references)
    /// - Multiple UV sets (UV0..UV3 best-effort)
    /// - Vertex color candidate
    /// - Tangents recalc when possible
    /// NOTE: Coordinate conversion for .mb meshes is applied elsewhere in the pipeline (mesh component ApplyToUnity).
    /// </summary>
    public static class MayaMbMeshDecoder
    {
        private const int MaxFloatRefs = 10;
        private const int MaxUIntRefs = 8;

        private struct ChunkRef
        {
            public int DataOffset;
            public int DataSize;
            public string Tag;
            public string Kind;
        }

        public static bool TryBuildUnityMesh(
            byte[] mbBytes,
            NodeRecord meshRecord,
            MayaImportOptions options,
            out Mesh mesh,
            out int[] expandedToOriginalVertex,
            out string note)
        {
            mesh = null;
            expandedToOriginalVertex = null;

            if (mbBytes == null || mbBytes.Length == 0)
            {
                note = "MB bytes empty.";
                return false;
            }

            if (meshRecord == null)
            {
                note = "Mesh NodeRecord null.";
                return false;
            }

            options ??= new MayaImportOptions();

            // 1) collect chunk refs attached by MayaMbMeshChunkLocator
            var floatRefs = new List<ChunkRef>(MaxFloatRefs);
            var uintRefs = new List<ChunkRef>(MaxUIntRefs);
            CollectChunkRefs(meshRecord, floatRefs, uintRefs);

            if (floatRefs.Count == 0 && uintRefs.Count == 0)
            {
                note = "No mesh chunk refs found on node (run MayaMbMeshChunkLocator in .mb parse pipeline).";
                return false;
            }

            // 2) decode float payloads (BE)
            var floatPayloads = new List<(ChunkRef r, float[] f)>(floatRefs.Count);
            for (int i = 0; i < floatRefs.Count; i++)
            {
                var r = floatRefs[i];
                if (!IsRangeValid(mbBytes, r.DataOffset, r.DataSize)) continue;
                if ((r.DataSize % 4) != 0) continue;

                int count = r.DataSize / 4;
                int cap = Math.Min(count, 5_000_000);

                var arr = ReadFloat32BEArray(mbBytes, r.DataOffset, cap);
                if (arr == null || arr.Length < 6) continue;

                floatPayloads.Add((r, arr));
            }

            if (floatPayloads.Count == 0)
            {
                note = "Float chunk refs exist but none decodable.";
                return false;
            }

            // 3) positions: best float3
            int bestPosIdx = -1;
            float bestPosScore = float.NegativeInfinity;
            int bestVertCount = 0;

            for (int i = 0; i < floatPayloads.Count; i++)
            {
                var f = floatPayloads[i].f;
                if ((f.Length % 3) != 0) continue;

                int vCount = f.Length / 3;
                if (vCount < 3) continue;

                float score = ScoreAsPositions(f);
                if (score > bestPosScore)
                {
                    bestPosScore = score;
                    bestPosIdx = i;
                    bestVertCount = vCount;
                }
            }

            if (bestPosIdx < 0)
            {
                note = "Could not find a plausible position(float3) chunk.";
                return false;
            }

            var posRef = floatPayloads[bestPosIdx].r;
            var posFloats = floatPayloads[bestPosIdx].f;
            var vertices = new Vector3[bestVertCount];
            for (int i = 0; i < bestVertCount; i++)
            {
                int o = i * 3;
                vertices[i] = new Vector3(posFloats[o + 0], posFloats[o + 1], posFloats[o + 2]);
            }

            // 4) UV candidates: float2 arrays with same vertCount
            var uvCandidates = new List<(int payloadIndex, float score)>();
            for (int i = 0; i < floatPayloads.Count; i++)
            {
                if (i == bestPosIdx) continue;

                var f = floatPayloads[i].f;
                if ((f.Length % 2) != 0) continue;

                int uvCount = f.Length / 2;
                if (uvCount != bestVertCount) continue;

                float score = ScoreAsUV(f);
                uvCandidates.Add((i, score));
            }

            uvCandidates.Sort((a, b) => b.score.CompareTo(a.score));

            var uvSets = new List<Vector2[]>(4);
            var uvRefs = new List<ChunkRef>(4);

            for (int ui = 0; ui < uvCandidates.Count && uvSets.Count < 4; ui++)
            {
                int idx = uvCandidates[ui].payloadIndex;
                var uvFloats = floatPayloads[idx].f;

                var uv = new Vector2[bestVertCount];
                for (int v = 0; v < bestVertCount; v++)
                {
                    int o = v * 2;
                    uv[v] = new Vector2(uvFloats[o + 0], uvFloats[o + 1]);
                }

                bool dup = false;
                for (int ex = 0; ex < uvSets.Count; ex++)
                {
                    if (AreUvSetsNearlyIdentical(uvSets[ex], uv))
                    {
                        dup = true;
                        break;
                    }
                }

                if (dup) continue;

                uvSets.Add(uv);
                uvRefs.Add(floatPayloads[idx].r);
            }

            Vector2[] uv0 = (uvSets.Count > 0) ? uvSets[0] : null;
            ChunkRef? uv0Ref = (uvRefs.Count > 0) ? uvRefs[0] : (ChunkRef?)null;

            // 4.5) normals: best float3 (same vertCount) not the same payload as positions
            Vector3[] normals = null;
            int nrmPicked = -1;
            float bestNrmScore = float.NegativeInfinity;

            for (int i = 0; i < floatPayloads.Count; i++)
            {
                if (i == bestPosIdx) continue;

                var f = floatPayloads[i].f;
                if ((f.Length % 3) != 0) continue;
                int nCount = f.Length / 3;
                if (nCount != bestVertCount) continue;

                float score = ScoreAsNormals(f);
                if (score > bestNrmScore)
                {
                    bestNrmScore = score;
                    nrmPicked = i;
                }
            }

            ChunkRef? nrmRef = null;
            if (nrmPicked >= 0 && bestNrmScore > -5f)
            {
                var nf = floatPayloads[nrmPicked].f;
                normals = new Vector3[bestVertCount];
                for (int i = 0; i < bestVertCount; i++)
                {
                    int o = i * 3;
                    normals[i] = new Vector3(nf[o + 0], nf[o + 1], nf[o + 2]);
                }
                nrmRef = floatPayloads[nrmPicked].r;
            }

            // 4.75) colors: float4 candidate
            Color[] colors = null;
            ChunkRef? colRef = null;
            float bestColScore = float.NegativeInfinity;

            for (int i = 0; i < floatPayloads.Count; i++)
            {
                if (i == bestPosIdx) continue;

                var f = floatPayloads[i].f;
                if ((f.Length % 4) != 0) continue;

                int cCount = f.Length / 4;
                if (cCount != bestVertCount) continue;

                float score = ScoreAsColors(f);
                if (score > bestColScore)
                {
                    bestColScore = score;
                    colRef = floatPayloads[i].r;

                    var col = new Color[bestVertCount];
                    for (int v = 0; v < bestVertCount; v++)
                    {
                        int o = v * 4;
                        col[v] = new Color(
                            Mathf.Clamp01(f[o + 0]),
                            Mathf.Clamp01(f[o + 1]),
                            Mathf.Clamp01(f[o + 2]),
                            Mathf.Clamp01(f[o + 3]));
                    }
                    colors = col;
                }
            }

            // 5) uint payloads -> indices (BE int32)
            var uintPayloads = new List<(ChunkRef r, int[] idx)>(uintRefs.Count);
            for (int i = 0; i < uintRefs.Count; i++)
            {
                var r = uintRefs[i];
                if (!IsRangeValid(mbBytes, r.DataOffset, r.DataSize)) continue;
                if ((r.DataSize % 4) != 0) continue;

                int count = r.DataSize / 4;
                int cap = Math.Min(count, 5_000_000);

                var arr = ReadInt32BEArray(mbBytes, r.DataOffset, cap);
                if (arr == null || arr.Length < 3) continue;

                uintPayloads.Add((r, arr));
            }

            int[] triangles = null;
            ChunkRef? triRef = null;
            bool usedPolyFaces = false;
            bool usedSequentialFallback = false;

            float bestTriScore = float.NegativeInfinity;

            for (int i = 0; i < uintPayloads.Count; i++)
            {
                var cand = uintPayloads[i].idx;
                var r = uintPayloads[i].r;

                // direct triangles
                if ((cand.Length % 3) == 0)
                {
                    var cleaned = CleanTriangleIndexArray(cand, bestVertCount);
                    float s = ScoreAsTriangleIndices(cleaned, bestVertCount);
                    if (s > bestTriScore)
                    {
                        bestTriScore = s;
                        triangles = cleaned;
                        triRef = r;
                        usedPolyFaces = false;
                    }
                }

                // polyFaces style
                if (cand.Length > 8 && HasNegativeSeparators(cand))
                {
                    if (TryConvertPolyFacesToTriangles(cand, bestVertCount, out var polyTri, out var faceCount))
                    {
                        var cleaned = CleanTriangleIndexArray(polyTri, bestVertCount);
                        float s = ScoreAsTriangleIndices(cleaned, bestVertCount) + Mathf.Log(1f + faceCount) * 0.5f;
                        if (s > bestTriScore)
                        {
                            bestTriScore = s;
                            triangles = cleaned;
                            triRef = r;
                            usedPolyFaces = true;
                        }
                    }
                }
            }

            if (triangles == null || triangles.Length < 3)
            {
                triangles = new int[bestVertCount];
                for (int i = 0; i < bestVertCount; i++) triangles[i] = i;
                usedSequentialFallback = true;
            }

            // 6) Build Unity Mesh
            var m = new Mesh
            {
                name = string.IsNullOrEmpty(meshRecord.Name) ? "MayaMbMesh" : (meshRecord.Name + "_mb"),
                indexFormat = (vertices.Length > 65535) ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            m.vertices = vertices;

            if (uvSets != null && uvSets.Count > 0)
            {
                if (uvSets[0] != null && uvSets[0].Length == vertices.Length)
                    m.uv = uvSets[0];

                for (int ch = 1; ch < uvSets.Count; ch++)
                {
                    var u = uvSets[ch];
                    if (u == null || u.Length != vertices.Length) continue;
                    m.SetUVs(ch, new List<Vector2>(u));
                }
            }

            if (normals != null && normals.Length == vertices.Length)
                m.normals = normals;

            if (colors != null && colors.Length == vertices.Length)
                m.colors = colors;

            if (triangles.Length >= 3 && (triangles.Length % 3) == 0 && !usedSequentialFallback)
            {
                m.subMeshCount = 1;
                m.SetTriangles(triangles, 0, calculateBounds: true);
            }
            else
            {
                m.subMeshCount = 1;
                m.SetIndices(triangles, MeshTopology.Points, 0, calculateBounds: true);
            }

            if (normals == null)
            {
                try { m.RecalculateNormals(); } catch { }
            }

            try
            {
                if (uv0 != null) m.RecalculateTangents();
            }
            catch { }

            m.RecalculateBounds();

            expandedToOriginalVertex = BuildIdentityMap(vertices.Length);
            mesh = m;

            note = MakeNote(meshRecord, posRef, uv0Ref, uvSets != null ? uvSets.Count : 0, colRef, nrmRef, triRef, usedPolyFaces, usedSequentialFallback, triangles.Length);
            return true;
        }

        // =========================================================
        // ChunkRef collection + parsing
        // =========================================================

        private static void CollectChunkRefs(NodeRecord rec, List<ChunkRef> floatRefs, List<ChunkRef> uintRefs)
        {
            if (rec?.Attributes == null) return;

            for (int i = 1; i <= MaxFloatRefs; i++)
            {
                string key = (i == 1) ? ".mbMeshFloatChunkRef" : $".mbMeshFloatChunkRef{i}";
                if (rec.Attributes.TryGetValue(key, out var v) && v != null && v.ValueTokens != null && v.ValueTokens.Count > 0)
                {
                    if (TryParseChunkRef(v.ValueTokens[0], out var r))
                        floatRefs.Add(r);
                }
            }

            for (int i = 1; i <= MaxUIntRefs; i++)
            {
                string key = (i == 1) ? ".mbMeshUIntChunkRef" : $".mbMeshUIntChunkRef{i}";
                if (rec.Attributes.TryGetValue(key, out var v) && v != null && v.ValueTokens != null && v.ValueTokens.Count > 0)
                {
                    if (TryParseChunkRef(v.ValueTokens[0], out var r))
                        uintRefs.Add(r);
                }
            }
        }

        private static bool TryParseChunkRef(string s, out ChunkRef r)
        {
            r = default;
            if (string.IsNullOrEmpty(s)) return false;

            // Supports both:
            // A) "TAG=...;KIND=...;DATA=...;SIZE=..."
            // B) "tag|kind|offset|size"
            if (s.IndexOf("DATA=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var parts = s.Split(';');
                int data = -1, size = -1;
                string tag = null, kind = null;

                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    int eq = p.IndexOf('=');
                    if (eq <= 0 || eq >= p.Length - 1) continue;

                    var k = p.Substring(0, eq).Trim();
                    var v = p.Substring(eq + 1).Trim();

                    if (k == "TAG") tag = v;
                    else if (k == "DATA" && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var di)) data = di;
                    else if (k == "SIZE" && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var si)) size = si;
                    else if (k == "KIND") kind = v;
                }

                if (data < 0 || size <= 0) return false;

                r = new ChunkRef { DataOffset = data, DataSize = size, Tag = tag, Kind = kind };
                return true;
            }
            else
            {
                var parts = s.Split('|');
                if (parts.Length < 4) return false;

                r.Tag = parts[0];
                r.Kind = parts[1];

                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.DataOffset))
                    return false;

                if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.DataSize))
                    return false;

                return r.DataOffset >= 0 && r.DataSize > 0;
            }
        }

        // =========================================================
        // Heuristics
        // =========================================================

        private static float ScoreAsPositions(float[] f)
        {
            if (f == null || f.Length < 9) return float.NegativeInfinity;

            int sample = Math.Min(f.Length, 9000);
            int finite = 0;
            int moderate = 0;

            for (int i = 0; i < sample; i++)
            {
                float v = f[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                finite++;
                if (Mathf.Abs(v) < 1_000_000f) moderate++;
            }

            if (finite <= 0) return float.NegativeInfinity;
            float ratio = (float)moderate / finite;

            return ratio * 10f + Mathf.Log(1f + f.Length) * 0.1f;
        }

        private static float ScoreAsUV(float[] f)
        {
            if (f == null || f.Length < 8) return float.NegativeInfinity;

            int sample = Math.Min(f.Length, 6000);
            int in01 = 0;
            int finite = 0;

            for (int i = 0; i < sample; i++)
            {
                float v = f[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                finite++;
                if (v >= -0.25f && v <= 1.25f) in01++;
            }

            if (finite <= 0) return float.NegativeInfinity;
            float ratio = (float)in01 / finite;

            return ratio * 10f + Mathf.Log(1f + f.Length) * 0.1f;
        }

        private static float ScoreAsColors(float[] f)
        {
            if (f == null || f.Length < 16) return float.NegativeInfinity;

            int sample = Math.Min(f.Length, 8000);
            int in01 = 0;
            int finite = 0;

            for (int i = 0; i < sample; i++)
            {
                float v = f[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                finite++;
                if (v >= -0.05f && v <= 1.05f) in01++;
            }

            if (finite <= 0) return float.NegativeInfinity;
            float ratio = (float)in01 / finite;

            return ratio * 10f + Mathf.Log(1f + f.Length) * 0.05f;
        }

        private static bool AreUvSetsNearlyIdentical(Vector2[] a, Vector2[] b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            if (a.Length != b.Length) return false;
            if (a.Length == 0) return true;

            int n = a.Length;
            int sample = Math.Min(64, n);
            double err = 0.0;

            int step = Math.Max(1, n / sample);
            int count = 0;

            for (int i = 0; i < n && count < sample; i += step, count++)
            {
                var d = a[i] - b[i];
                err += (double)d.sqrMagnitude;
            }

            double mse = err / Math.Max(1, count);
            return mse < 1e-12;
        }

        private static float ScoreAsNormals(float[] f)
        {
            if (f == null || f.Length < 9) return float.NegativeInfinity;

            int sample = Math.Min(f.Length, 9000);
            int inRange = 0;
            int finite = 0;

            for (int i = 0; i < sample; i++)
            {
                float v = f[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) continue;
                finite++;
                if (v >= -1.5f && v <= 1.5f) inRange++;
            }

            if (finite <= 0) return float.NegativeInfinity;
            float ratio = (float)inRange / finite;

            return ratio * 10f + Mathf.Log(1f + f.Length) * 0.1f;
        }

        private static float ScoreAsTriangleIndices(int[] idx, int vertexCount)
        {
            if (idx == null || idx.Length < 3) return float.NegativeInfinity;
            if ((idx.Length % 3) != 0) return float.NegativeInfinity;

            int sampleTris = Math.Min(idx.Length / 3, 20000);
            int ok = 0;
            int deg = 0;

            for (int t = 0; t < sampleTris; t++)
            {
                int o = t * 3;
                int a = idx[o + 0];
                int b = idx[o + 1];
                int c = idx[o + 2];

                if (a < 0 || b < 0 || c < 0) continue;
                if (a >= vertexCount || b >= vertexCount || c >= vertexCount) continue;

                if (a == b || b == c || c == a) { deg++; continue; }
                ok++;
            }

            return ok * 1f - deg * 0.5f + Mathf.Log(1f + idx.Length) * 0.05f;
        }

        // =========================================================
        // polyFaces conversion
        // =========================================================

        private static bool HasNegativeSeparators(int[] arr)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
                if (arr[i] < 0) return true;
            return false;
        }

        private static bool TryConvertPolyFacesToTriangles(int[] polyFaces, int vertexCount, out int[] triangles, out int faceCount)
        {
            triangles = null;
            faceCount = 0;

            if (polyFaces == null || polyFaces.Length < 4)
                return false;

            var tris = new List<int>(polyFaces.Length);
            int i = 0;

            while (i < polyFaces.Length)
            {
                var face = new List<int>(8);

                while (i < polyFaces.Length)
                {
                    int v = polyFaces[i++];
                    if (v < 0)
                    {
                        int last = (-v) - 1;
                        if (last >= 0) face.Add(last);
                        break;
                    }
                    face.Add(v);
                }

                if (face.Count >= 3)
                {
                    int a = face[0];
                    for (int k = 1; k + 1 < face.Count; k++)
                    {
                        int b = face[k];
                        int c = face[k + 1];

                        if (a < 0 || b < 0 || c < 0) continue;
                        if (a >= vertexCount || b >= vertexCount || c >= vertexCount) continue;

                        tris.Add(a); tris.Add(b); tris.Add(c);
                    }
                }

                faceCount++;
            }

            if (tris.Count < 3) return false;
            triangles = tris.ToArray();
            return true;
        }

        private static int[] CleanTriangleIndexArray(int[] src, int vertexCount)
        {
            if (src == null || src.Length < 3) return Array.Empty<int>();
            if ((src.Length % 3) != 0) return Array.Empty<int>();

            var dst = new List<int>(src.Length);

            for (int i = 0; i < src.Length; i += 3)
            {
                int a = src[i + 0];
                int b = src[i + 1];
                int c = src[i + 2];

                if (a < 0 || b < 0 || c < 0) continue;
                if (a >= vertexCount || b >= vertexCount || c >= vertexCount) continue;
                if (a == b || b == c || c == a) continue;

                dst.Add(a); dst.Add(b); dst.Add(c);
            }

            return dst.Count > 0 ? dst.ToArray() : Array.Empty<int>();
        }

        // =========================================================
        // Binary readers (BE)
        // =========================================================

        private static float[] ReadFloat32BEArray(byte[] bytes, int offset, int count)
        {
            if (bytes == null) return null;
            if (count <= 0) return Array.Empty<float>();
            if (offset < 0 || offset + count * 4 > bytes.Length) return null;

            var arr = new float[count];
            int o = offset;

            for (int i = 0; i < count; i++)
            {
                uint u = (uint)((bytes[o] << 24) | (bytes[o + 1] << 16) | (bytes[o + 2] << 8) | bytes[o + 3]);
                var le = BitConverter.GetBytes(u);
                float f = BitConverter.ToSingle(le, 0);
                if (float.IsNaN(f) || float.IsInfinity(f)) f = 0f;
                arr[i] = f;
                o += 4;
            }

            return arr;
        }

        private static int[] ReadInt32BEArray(byte[] bytes, int offset, int count)
        {
            if (bytes == null) return null;
            if (count <= 0) return Array.Empty<int>();
            if (offset < 0 || offset + count * 4 > bytes.Length) return null;

            var arr = new int[count];
            int o = offset;

            for (int i = 0; i < count; i++)
            {
                int v = (bytes[o] << 24) | (bytes[o + 1] << 16) | (bytes[o + 2] << 8) | bytes[o + 3];
                arr[i] = v;
                o += 4;
            }

            return arr;
        }

        private static bool IsRangeValid(byte[] bytes, int dataOffset, int dataSize)
        {
            return bytes != null &&
                   dataOffset >= 0 &&
                   dataSize > 0 &&
                   dataOffset + dataSize <= bytes.Length;
        }

        private static int[] BuildIdentityMap(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            return a;
        }

        private static string MakeNote(NodeRecord rec, ChunkRef posRef, ChunkRef? uv0Ref, int uvSetCount, ChunkRef? colRef, ChunkRef? nrmRef, ChunkRef? triRef, bool usedPolyFaces, bool seqFallback, int triLen)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("Built Mesh (best-effort). ");
            sb.Append("node=").Append(rec?.Name ?? "null").Append(". ");

            sb.Append("posChunk=");
            sb.Append($"{posRef.Tag}@{posRef.DataOffset}+{posRef.DataSize}");

            sb.Append(" uvSets=");
            sb.Append(uvSetCount.ToString(CultureInfo.InvariantCulture));

            sb.Append(" uv0Chunk=");
            sb.Append(uv0Ref.HasValue ? $"{uv0Ref.Value.Tag}@{uv0Ref.Value.DataOffset}+{uv0Ref.Value.DataSize}" : "(none)");

            sb.Append(" colChunk=");
            sb.Append(colRef.HasValue ? $"{colRef.Value.Tag}@{colRef.Value.DataOffset}+{colRef.Value.DataSize}" : "(none)");

            sb.Append(" nrmChunk=");
            sb.Append(nrmRef.HasValue ? $"{nrmRef.Value.Tag}@{nrmRef.Value.DataOffset}+{nrmRef.Value.DataSize}" : "(none)");

            sb.Append(" idx=");
            sb.Append(triLen.ToString(CultureInfo.InvariantCulture));

            sb.Append(" idxChunk=");
            sb.Append(triRef.HasValue ? $"{triRef.Value.Tag}@{triRef.Value.DataOffset}+{triRef.Value.DataSize}" : "(none)");

            if (usedPolyFaces) sb.Append(" [polyFaces->triangulated]");
            if (seqFallback) sb.Append(" [sequentialFallback]");

            return sb.ToString();
        }
    }
}
