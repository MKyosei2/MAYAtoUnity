using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// .mb(Binary) -> Unity Mesh best-effort decoder.
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

            // 2) decode float payload candidates
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

            // 3) choose best position candidate (float3)
            int bestPosIdx = -1;
            float bestPosScore = float.NegativeInfinity;
            int bestVertCount = 0;

            for (int i = 0; i < floatPayloads.Count; i++)
            {
                var f = floatPayloads[i].f;
                if ((f.Length % 3) != 0) continue;

                int vCount = f.Length / 3;
                if (vCount < 3) continue;

                var score = ScoreAsPositions(f);
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

            // 4) choose UV candidate (float2) if any
            Vector2[] uv0 = null;
            int uvPicked = -1;
            float bestUvScore = float.NegativeInfinity;

            for (int i = 0; i < floatPayloads.Count; i++)
            {
                if (i == bestPosIdx) continue;

                var f = floatPayloads[i].f;
                if ((f.Length % 2) != 0) continue;

                int uvCount = f.Length / 2;
                if (uvCount != bestVertCount) continue;

                float score = ScoreAsUV(f);
                if (score > bestUvScore)
                {
                    bestUvScore = score;
                    uvPicked = i;
                }
            }

            ChunkRef? uvRef = null;
            if (uvPicked >= 0)
            {
                var uvFloats = floatPayloads[uvPicked].f;
                uv0 = new Vector2[bestVertCount];
                for (int i = 0; i < bestVertCount; i++)
                {
                    int o = i * 2;
                    uv0[i] = new Vector2(uvFloats[o + 0], uvFloats[o + 1]);
                }
                uvRef = floatPayloads[uvPicked].r;
            }

            // 4.5) choose Normal candidate (float3, same vertCount)
            Vector3[] normals = null;
            int nrmPicked = -1;
            float bestNrmScore = float.NegativeInfinity;

            for (int i = 0; i < floatPayloads.Count; i++)
            {
                if (i == bestPosIdx) continue;
                if (i == uvPicked) continue;

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

            // 5) decode uint payload candidates -> indices (triangles or polyFaces)
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

                // A) direct triangles
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

                // B) polyFaces-like (negatives as face terminators)
                if (LooksLikePolyFaces(cand))
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

            // 6) if no indices found, fallback
            if (triangles == null || triangles.Length < 3)
            {
                triangles = new int[bestVertCount];
                for (int i = 0; i < bestVertCount; i++) triangles[i] = i;
                usedSequentialFallback = true;
            }

            // 7) build Unity mesh
            var m = new Mesh
            {
                name = string.IsNullOrEmpty(meshRecord.Name) ? "MayaMbMesh" : (meshRecord.Name + "_mb")
            };

            m.indexFormat = (vertices.Length > 65535)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            m.vertices = vertices;

            if (uv0 != null && uv0.Length == vertices.Length)
                m.uv = uv0;

            if (normals != null && normals.Length == vertices.Length)
                m.normals = normals;

            // topology
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

            // if we didn't set normals explicitly, recalc
            if (normals == null)
            {
                try { m.RecalculateNormals(); } catch { }
            }

            m.RecalculateBounds();

            expandedToOriginalVertex = BuildIdentityMap(vertices.Length);
            mesh = m;

            note = MakeNote(meshRecord, posRef, uvRef, nrmRef, triRef, usedPolyFaces, usedSequentialFallback, triangles.Length);
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

            r = new ChunkRef
            {
                DataOffset = data,
                DataSize = size,
                Tag = tag,
                Kind = kind
            };
            return true;
        }

        // =========================================================
        // Heuristics
        // =========================================================

        private static float ScoreAsPositions(float[] f)
        {
            if (f == null || f.Length < 9) return float.NegativeInfinity;

            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            int neg = 0;
            int in01 = 0;
            int inN1P1 = 0;

            int sample = Math.Min(f.Length, 6000);
            for (int i = 0; i < sample; i++)
            {
                float v = f[i];
                if (v < min) min = v;
                if (v > max) max = v;
                if (v < 0f) neg++;
                if (v >= 0f && v <= 1f) in01++;
                if (v >= -1f && v <= 1f) inN1P1++;
            }

            float range = max - min;
            float negRatio = sample > 0 ? (float)neg / sample : 0f;
            float in01Ratio = sample > 0 ? (float)in01 / sample : 0f;
            float inN1P1Ratio = sample > 0 ? (float)inN1P1 / sample : 0f;

            float score = 0f;
            score += Mathf.Log(1f + Mathf.Abs(range)) * 6f;
            score += negRatio * 4f;
            score -= in01Ratio * 6f;
            score -= inN1P1Ratio * 2f;
            score += Mathf.Log(1f + f.Length) * 0.3f;

            return score;
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

        private static float ScoreAsNormals(float[] f)
        {
            // normals: values mostly in [-1..1], magnitude around 1 (not strict)
            if (f == null || f.Length < 9) return float.NegativeInfinity;

            int sampleVec = Math.Min(f.Length / 3, 2000);
            if (sampleVec <= 0) return float.NegativeInfinity;

            int inRange = 0;
            float magErr = 0f;

            for (int i = 0; i < sampleVec; i++)
            {
                int o = i * 3;
                float x = f[o + 0];
                float y = f[o + 1];
                float z = f[o + 2];

                if (x >= -1.2f && x <= 1.2f &&
                    y >= -1.2f && y <= 1.2f &&
                    z >= -1.2f && z <= 1.2f)
                    inRange++;

                float m = Mathf.Sqrt(x * x + y * y + z * z);
                magErr += Mathf.Abs(1f - m);
            }

            float inRangeRatio = (float)inRange / sampleVec;
            float avgMagErr = magErr / sampleVec;

            // higher is better
            float score = 0f;
            score += inRangeRatio * 10f;
            score -= avgMagErr * 6f;
            score += Mathf.Log(1f + f.Length) * 0.05f;
            return score;
        }

        private static float ScoreAsTriangleIndices(int[] idx, int vertexCount)
        {
            if (idx == null || idx.Length < 3 || vertexCount <= 0) return float.NegativeInfinity;
            if ((idx.Length % 3) != 0) return float.NegativeInfinity;

            int sample = Math.Min(idx.Length, 12000);
            int valid = 0;
            int neg = 0;
            int maxv = 0;

            for (int i = 0; i < sample; i++)
            {
                int v = idx[i];
                if (v < 0) { neg++; continue; }
                if (v < vertexCount) valid++;
                if (v > maxv) maxv = v;
            }

            float validRatio = sample > 0 ? (float)valid / sample : 0f;
            float negRatio = sample > 0 ? (float)neg / sample : 0f;

            float score = 0f;
            score += validRatio * 12f;
            score -= negRatio * 20f;

            if (maxv > vertexCount * 4) score -= 8f;
            score += Mathf.Log(1f + idx.Length) * 0.4f;

            return score;
        }

        // =========================================================
        // polyFaces-like conversion
        // =========================================================

        private static bool LooksLikePolyFaces(int[] idx)
        {
            if (idx == null || idx.Length < 8) return false;

            int sample = Math.Min(idx.Length, 4096);
            int neg = 0;
            for (int i = 0; i < sample; i++)
                if (idx[i] < 0) neg++;

            // if there are some negatives, it's a candidate
            return neg >= 2;
        }

        private static bool TryConvertPolyFacesToTriangles(int[] poly, int vertexCount, out int[] triangles, out int faceCount)
        {
            triangles = null;
            faceCount = 0;
            if (poly == null || poly.Length < 4) return false;

            var tris = new List<int>(poly.Length);

            var face = new List<int>(16);
            for (int i = 0; i < poly.Length; i++)
            {
                int v = poly[i];

                if (v >= 0)
                {
                    face.Add(v);
                    continue;
                }

                // end-of-face marker: Maya style often uses -(v+1)
                int last = (-v) - 1;
                face.Add(last);

                // triangulate fan
                if (face.Count >= 3)
                {
                    int a = face[0];
                    for (int k = 1; k + 1 < face.Count; k++)
                    {
                        int b = face[k];
                        int c = face[k + 1];

                        // keep even if out of range; will be cleaned later
                        tris.Add(a);
                        tris.Add(b);
                        tris.Add(c);
                    }

                    faceCount++;
                }

                face.Clear();
            }

            // trailing (no terminator) face
            if (face.Count >= 3)
            {
                int a = face[0];
                for (int k = 1; k + 1 < face.Count; k++)
                {
                    tris.Add(a);
                    tris.Add(face[k]);
                    tris.Add(face[k + 1]);
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
        // Decoders
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

        private static string MakeNote(NodeRecord rec, ChunkRef posRef, ChunkRef? uvRef, ChunkRef? nrmRef, ChunkRef? triRef, bool usedPolyFaces, bool seqFallback, int triLen)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("Built Mesh (best-effort). ");
            sb.Append("node=").Append(rec?.Name ?? "null").Append(". ");

            sb.Append("posChunk=");
            sb.Append($"{posRef.Tag}@{posRef.DataOffset}+{posRef.DataSize}");

            sb.Append(" uvChunk=");
            sb.Append(uvRef.HasValue ? $"{uvRef.Value.Tag}@{uvRef.Value.DataOffset}+{uvRef.Value.DataSize}" : "(none)");

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
