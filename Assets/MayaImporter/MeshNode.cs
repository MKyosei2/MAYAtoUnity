// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Represents a Maya mesh node and rebuilds a Unity Mesh.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MeshNode : MonoBehaviour
    {
        public MeshReader reader;

        public Mesh BuildMesh()
        {
            if (reader == null || reader.vertexReader == null || reader.topologyReader == null)
                return null;

            Mesh mesh = new Mesh
            {
                name = gameObject.name
            };

            mesh.vertices = reader.vertexReader.vertices;
            mesh.triangles = reader.topologyReader.triangles;

            if (reader.normalReader != null && reader.normalReader.normals != null)
                mesh.normals = reader.normalReader.normals;
            else
                mesh.RecalculateNormals();

            if (reader.tangentReader != null && reader.tangentReader.tangents != null)
                mesh.tangents = reader.tangentReader.tangents;

            if (reader.uvReader != null && reader.uvReader.uvs != null)
                mesh.uv = reader.uvReader.uvs;

            if (reader.colorReader != null && reader.colorReader.colors != null)
                mesh.colors = reader.colorReader.colors;

            mesh.RecalculateBounds();
            return mesh;
        }

        private void Awake()
        {
            reader = GetComponent<MeshReader>();
            var mesh = BuildMesh();
            if (mesh != null)
            {
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
        }
    }
}
