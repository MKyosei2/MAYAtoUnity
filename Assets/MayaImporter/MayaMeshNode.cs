// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya Mesh m[h1ɑΉ Unity NX
    /// MayaCXg[ Mesh č\zӖ
    /// </summary>
    public class MayaMeshNode : MonoBehaviour
    {
        [Header("Maya Mesh Info")]
        public string mayaNodeName;

        [Header("Mesh Data")]
        public Vector3[] vertices;
        public int[] triangles;
        public Vector3[] normals;
        public Vector2[] uvs;

        /// <summary>
        /// Maya Mesh 񂩂 Unity Mesh 𐶐
        /// </summary>
        public Mesh BuildMesh()
        {
            var mesh = new Mesh
            {
                name = mayaNodeName + "_Mesh"
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;

            if (normals != null && normals.Length == vertices.Length)
                mesh.normals = normals;
            else
                mesh.RecalculateNormals();

            if (uvs != null && uvs.Length == vertices.Length)
                mesh.uv = uvs;

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Mesh  GameObject ɓKp
        /// </summary>
        public void Apply()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();

            meshFilter.sharedMesh = BuildMesh();
        }
    }
}
