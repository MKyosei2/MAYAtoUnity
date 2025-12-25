using UnityEngine;
using MayaImporter.Components;
using MayaImporter.Geometry;

namespace MayaImporter.Binder
{
    /// <summary>
    /// Synchronizes blend shape channel weights to Unity SkinnedMeshRenderer.
    /// - Prefer BlendShapeChannelNode (created by reconstruction)
    /// - Fallback to MayaBlendShapeMetadata targets
    /// </summary>
    [DisallowMultipleComponent]
    public class BlendShapeWeightBinder : MonoBehaviour
    {
        public SkinnedMeshRenderer skinnedRenderer;
        public MayaBlendShapeMetadata metadata;
        public GameObject blendShapeNodeObject; // optional: where channels live

        public void ApplyWeights()
        {
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
                return;

            var mesh = skinnedRenderer.sharedMesh;

            // 1) Prefer channel nodes (Unity indices already resolved)
            if (blendShapeNodeObject != null)
            {
                var channels = blendShapeNodeObject.GetComponentsInChildren<BlendShapeChannelNode>(true);
                if (channels != null && channels.Length > 0)
                {
                    for (int i = 0; i < channels.Length; i++)
                    {
                        var ch = channels[i];
                        if (ch == null) continue;
                        if (ch.targetIndex < 0 || ch.targetIndex >= mesh.blendShapeCount) continue;

                        skinnedRenderer.SetBlendShapeWeight(ch.targetIndex, Mathf.Clamp(ch.weight * 100f, 0f, 100f));
                    }
                    return;
                }
            }

            // 2) Fallback: metadata -> find by name
            if (metadata == null || metadata.targets == null) return;

            for (int i = 0; i < metadata.targets.Count; i++)
            {
                var t = metadata.targets[i];
                if (string.IsNullOrEmpty(t.name)) continue;

                int idx = mesh.GetBlendShapeIndex(t.name);
                if (idx < 0) continue;

                skinnedRenderer.SetBlendShapeWeight(idx, Mathf.Clamp(t.weight * 100f, 0f, 100f));
            }
        }
    }
}
