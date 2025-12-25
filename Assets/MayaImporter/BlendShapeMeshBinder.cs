using UnityEngine;
using MayaImporter.Geometry;
using MayaImporter.Deformers;

namespace MayaImporter.Binder
{
    /// <summary>
    /// Binds Maya blendShape node data to a Unity Mesh (legacy helper).
    /// NOTE:
    /// - Current pipeline already reconstructs blendshapes via MayaBlendShapeReconstruction.
    /// - This binder is kept for manual / debug usage and must compile without Maya/API.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlendShapeMeshBinder : MonoBehaviour
    {
        public BlendShapeNode blendShapeNode;
        public MeshNode targetMeshNode;

        public void Bind()
        {
            if (blendShapeNode == null || targetMeshNode == null)
                return;

            var meshFilter = targetMeshNode.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return;

            Mesh mesh = meshFilter.sharedMesh;

            var targetNodes = blendShapeNode.GetComponentsInChildren<BlendShapeTargetNode>(true);
            if (targetNodes == null || targetNodes.Length == 0)
                return;

            foreach (var target in targetNodes)
            {
                if (target == null) continue;
                if (target.deltaVertices == null) continue;

                mesh.AddBlendShapeFrame(
                    target.targetName ?? "target",
                    100.0f,
                    target.deltaVertices,
                    target.deltaNormals,
                    null
                );
            }
        }
    }
}
