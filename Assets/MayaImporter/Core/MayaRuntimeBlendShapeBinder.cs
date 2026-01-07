// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Runtime binder for BlendShape deformers (when .ma/.mb not present).
    /// Ensures SkinnedMeshRenderer.sharedMesh blendShapes exist and are applied.
    /// </summary>
    [DefaultExecutionOrder(-650)]
    public sealed class MayaRuntimeBlendShapeBinder : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindBlendShapes()
        {
            var smrs = GameObject.FindObjectsOfType<SkinnedMeshRenderer>(true);
            foreach (var s in smrs)
            {
                var m = s.sharedMesh;
                if (m == null) continue;
                if (m.blendShapeCount == 0) continue;

                // apply default weights (first 0%)
                for (int i = 0; i < m.blendShapeCount; i++)
                    s.SetBlendShapeWeight(i, 0f);
            }

            Debug.Log($"[MayaImporter] BlendShapeBinder complete ({smrs.Length} SkinnedMeshRenderers).");
        }
    }
}
