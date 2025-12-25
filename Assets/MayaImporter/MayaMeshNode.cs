using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Maya Mesh ノード1つに対応する Unity 側クラス
    /// Maya未インストール環境で Mesh を再構築する責務を持つ
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
        /// Maya Mesh 情報から Unity Mesh を生成
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
        /// Mesh を GameObject に適用
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
