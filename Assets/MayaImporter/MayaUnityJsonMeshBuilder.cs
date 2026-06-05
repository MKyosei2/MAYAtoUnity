// MAYAIMPORTER_PATCH_V7: Build Unity Mesh assets from Maya exporter JSON topology + skin/blendshape data
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    public static class MayaUnityJsonMeshBuilder
    {
        public static bool HasTopology(MayaUnityExportMesh mesh)
        {
            return mesh != null
                && mesh.vertices != null && mesh.vertices.Length >= 9
                && mesh.indices != null && mesh.indices.Length >= 3;
        }

        public static bool HasSkinning(MayaUnityExportMesh mesh)
        {
            return mesh != null
                && mesh.skinJoints != null && mesh.skinJoints.Length > 0
                && mesh.boneIndices != null && mesh.boneWeights != null
                && mesh.boneIndices.Length >= mesh.vertexCount * 4
                && mesh.boneWeights.Length >= mesh.vertexCount * 4;
        }

        public static Mesh BuildMesh(MayaUnityExportMesh src, MayaImportOptions options, MayaImportLog log)
        {
            if (!HasTopology(src))
            {
                log?.Warn("JSON mesh has no topology: " + (src != null ? src.name : "null"));
                return null;
            }

            int vertexCount = src.vertices.Length / 3;
            var vertices = new List<Vector3>(vertexCount);
            for (int i = 0; i + 2 < src.vertices.Length; i += 3)
            {
                float x = src.vertices[i + 0];
                float y = src.vertices[i + 1];
                float z = src.vertices[i + 2];
                if (options != null && options.Conversion == CoordinateConversion.MayaToUnity_MirrorZ) z = -z;
                vertices.Add(new Vector3(x, y, z));
            }

            var indices = new List<int>(src.indices.Length);
            if (options != null && options.Conversion == CoordinateConversion.MayaToUnity_MirrorZ)
            {
                for (int i = 0; i + 2 < src.indices.Length; i += 3)
                {
                    indices.Add(src.indices[i + 0]);
                    indices.Add(src.indices[i + 2]);
                    indices.Add(src.indices[i + 1]);
                }
            }
            else
            {
                indices.AddRange(src.indices);
            }

            var mesh = new Mesh();
            mesh.name = string.IsNullOrEmpty(src.name) ? "MayaMesh" : src.name;
#if UNITY_2017_3_OR_NEWER
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#endif
            mesh.SetVertices(vertices);

            if (src.uvs != null && src.uvs.Length / 2 == vertices.Count)
            {
                var uvs = new List<Vector2>(vertices.Count);
                for (int i = 0; i + 1 < src.uvs.Length; i += 2)
                    uvs.Add(new Vector2(src.uvs[i], src.uvs[i + 1]));
                mesh.SetUVs(0, uvs);
            }

            mesh.SetTriangles(indices, 0);

            if (src.normals != null && src.normals.Length / 3 == vertices.Count)
            {
                var normals = new List<Vector3>(vertices.Count);
                for (int i = 0; i + 2 < src.normals.Length; i += 3)
                {
                    float x = src.normals[i + 0];
                    float y = src.normals[i + 1];
                    float z = src.normals[i + 2];
                    if (options != null && options.Conversion == CoordinateConversion.MayaToUnity_MirrorZ) z = -z;
                    normals.Add(new Vector3(x, y, z).normalized);
                }
                mesh.SetNormals(normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }

            ApplyBoneWeights(mesh, src, vertices.Count, log);
            ApplyBlendShapes(mesh, src, options, vertices.Count, log);

            mesh.RecalculateBounds();
            try { mesh.RecalculateTangents(); } catch { }

            log?.Info("Built Unity Mesh from Maya JSON: " + mesh.name + " vertices=" + vertices.Count + " triangles=" + (indices.Count / 3));
            return mesh;
        }

        private static void ApplyBoneWeights(Mesh mesh, MayaUnityExportMesh src, int vertexCount, MayaImportLog log)
        {
            if (!HasSkinning(src)) return;
            var weights = new BoneWeight[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                int baseIndex = v * 4;
                var bw = new BoneWeight();
                bw.boneIndex0 = SafeBoneIndex(src.boneIndices, baseIndex + 0);
                bw.boneIndex1 = SafeBoneIndex(src.boneIndices, baseIndex + 1);
                bw.boneIndex2 = SafeBoneIndex(src.boneIndices, baseIndex + 2);
                bw.boneIndex3 = SafeBoneIndex(src.boneIndices, baseIndex + 3);
                bw.weight0 = SafeWeight(src.boneWeights, baseIndex + 0);
                bw.weight1 = SafeWeight(src.boneWeights, baseIndex + 1);
                bw.weight2 = SafeWeight(src.boneWeights, baseIndex + 2);
                bw.weight3 = SafeWeight(src.boneWeights, baseIndex + 3);
                Normalize(ref bw);
                weights[v] = bw;
            }
            mesh.boneWeights = weights;
            log?.Info("Applied bone weights to mesh: " + mesh.name + " vertices=" + vertexCount);
        }

        private static void ApplyBlendShapes(Mesh mesh, MayaUnityExportMesh src, MayaImportOptions options, int vertexCount, MayaImportLog log)
        {
            if (src == null || src.blendShapes == null) return;
            int count = 0;
            foreach (var bs in src.blendShapes)
            {
                if (bs == null || string.IsNullOrEmpty(bs.name) || bs.deltaVertices == null || bs.deltaVertices.Length / 3 != vertexCount)
                    continue;

                Vector3[] dv = ToVector3Array(bs.deltaVertices, vertexCount, options, true);
                Vector3[] dn = bs.deltaNormals != null && bs.deltaNormals.Length / 3 == vertexCount
                    ? ToVector3Array(bs.deltaNormals, vertexCount, options, true)
                    : new Vector3[vertexCount];
                Vector3[] dt = bs.deltaTangents != null && bs.deltaTangents.Length / 3 == vertexCount
                    ? ToVector3Array(bs.deltaTangents, vertexCount, options, true)
                    : new Vector3[vertexCount];

                float weight = bs.weight <= 0f ? 100f : bs.weight;
                mesh.AddBlendShapeFrame(bs.name, weight, dv, dn, dt);
                count++;
            }
            if (count > 0) log?.Info("Applied blendshapes to mesh: " + mesh.name + " count=" + count);
        }

        private static Vector3[] ToVector3Array(float[] src, int vertexCount, MayaImportOptions options, bool vectorDelta)
        {
            var dst = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                int j = i * 3;
                float x = src[j + 0];
                float y = src[j + 1];
                float z = src[j + 2];
                if (options != null && options.Conversion == CoordinateConversion.MayaToUnity_MirrorZ) z = -z;
                dst[i] = new Vector3(x, y, z);
            }
            return dst;
        }

        private static int SafeBoneIndex(int[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length) return 0;
            return Mathf.Max(0, values[index]);
        }

        private static float SafeWeight(float[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length) return 0f;
            return Mathf.Clamp01(values[index]);
        }

        private static void Normalize(ref BoneWeight bw)
        {
            float sum = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
            if (sum <= 0f)
            {
                bw.boneIndex0 = 0;
                bw.weight0 = 1f;
                bw.boneIndex1 = bw.boneIndex2 = bw.boneIndex3 = 0;
                bw.weight1 = bw.weight2 = bw.weight3 = 0f;
                return;
            }
            bw.weight0 /= sum;
            bw.weight1 /= sum;
            bw.weight2 /= sum;
            bw.weight3 /= sum;
        }
    }
}
