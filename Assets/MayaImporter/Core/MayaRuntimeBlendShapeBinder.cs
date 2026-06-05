// MAYAIMPORTER_PATCH_V14: BlendShape binder using Unity-version-safe object discovery
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Runtime binder for BlendShape deformers.
    /// Ensures SkinnedMeshRenderer.sharedMesh blendShapes exist and are initialized.
    /// </summary>
    [DefaultExecutionOrder(-650)]
    public sealed class MayaRuntimeBlendShapeBinder : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindBlendShapes()
        {
            var smrs = MayaRuntimeObjectFinder.FindAll<SkinnedMeshRenderer>();
            foreach (var s in smrs)
            {
                if (s == null) continue;
                var m = s.sharedMesh;
                if (m == null) continue;
                if (m.blendShapeCount == 0) continue;

                for (int i = 0; i < m.blendShapeCount; i++)
                    s.SetBlendShapeWeight(i, 0f);
            }

            Debug.Log("[MayaImporter] BlendShapeBinder complete (" + smrs.Length + " SkinnedMeshRenderers).");
        }
    }
}
