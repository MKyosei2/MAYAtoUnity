// MAYAIMPORTER_PATCH_V5: Build Unity Mesh assets from Maya exporter JSON topology
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
                // Mirroring one axis flips handedness. Reverse triangle winding.
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

            mesh.RecalculateBounds();
            try { mesh.RecalculateTangents(); } catch { }

            log?.Info("Built Unity Mesh from Maya JSON: " + mesh.name + " vertices=" + vertices.Count + " triangles=" + (indices.Count / 3));
            return mesh;
        }
    }
}
