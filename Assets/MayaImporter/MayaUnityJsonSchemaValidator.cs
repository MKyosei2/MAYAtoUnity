// MAYAIMPORTER_VALIDATION: Lightweight exporter JSON schema validation
using System.Collections.Generic;

namespace MayaImporter.Core
{
    public sealed class MayaSchemaValidationResult
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public bool Success { get { return Errors.Count == 0; } }
    }

    public static class MayaUnityJsonSchemaValidator
    {
        public static MayaSchemaValidationResult Validate(MayaUnityExport export)
        {
            var result = new MayaSchemaValidationResult();
            if (export == null)
            {
                result.Errors.Add("Exporter JSON root is null.");
                return result;
            }

            if (export.meshes != null)
            {
                for (int i = 0; i < export.meshes.Length; i++)
                {
                    ValidateMesh(export.meshes[i], i, result);
                }
            }
            else
            {
                result.Warnings.Add("JSON contains no mesh array. Hierarchy-only import may still be valid.");
            }

            if (export.materials == null || export.materials.Length == 0)
                result.Warnings.Add("JSON contains no material array.");

            if (export.nodes == null || export.nodes.Length == 0)
                result.Warnings.Add("JSON contains no node array; transform lookup will rely on fallback root where possible.");

            return result;
        }

        private static void ValidateMesh(MayaUnityExportMesh mesh, int index, MayaSchemaValidationResult result)
        {
            if (mesh == null)
            {
                result.Errors.Add("meshes[" + index + "] is null.");
                return;
            }

            int vertexCount = mesh.vertices != null ? mesh.vertices.Length / 3 : 0;
            if (vertexCount <= 0)
                result.Warnings.Add("mesh[" + index + "] has no vertices: " + mesh.name);

            if (mesh.triangles != null)
            {
                if (mesh.triangles.Length % 3 != 0)
                    result.Errors.Add("mesh[" + index + "] triangle index count is not divisible by 3: " + mesh.name);
                for (int i = 0; i < mesh.triangles.Length; i++)
                {
                    int idx = mesh.triangles[i];
                    if (idx < 0 || idx >= vertexCount)
                    {
                        result.Errors.Add("mesh[" + index + "] triangle index out of range at " + i + ": " + idx + " vertexCount=" + vertexCount);
                        break;
                    }
                }
            }

            if (mesh.normals != null && mesh.normals.Length > 0 && mesh.normals.Length != vertexCount * 3)
                result.Errors.Add("mesh[" + index + "] normals length does not match vertex count: " + mesh.name);

            if (mesh.uvs != null && mesh.uvs.Length > 0 && mesh.uvs.Length != vertexCount * 2)
                result.Errors.Add("mesh[" + index + "] uv length does not match vertex count: " + mesh.name);

            if (mesh.blendShapes != null)
            {
                for (int b = 0; b < mesh.blendShapes.Length; b++)
                {
                    MayaUnityExportBlendShape blend = mesh.blendShapes[b];
                    if (blend == null || blend.deltaVertices == null) continue;
                    if (blend.deltaVertices.Length != vertexCount * 3)
                        result.Errors.Add("mesh[" + index + "] blendShape[" + b + "] delta vertex count does not match mesh vertices: " + mesh.name);
                }
            }
        }
    }
}
