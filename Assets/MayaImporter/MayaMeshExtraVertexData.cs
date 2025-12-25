using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Unity Mesh has native limits:
    /// - UV channels: 0..7 (8 sets)
    /// - Vertex colors: single colors array
    ///
    /// This component preserves *all* Maya UV sets and *all* Maya color sets for 100% import fidelity.
    /// The main MayaMeshNodeComponent applies the first 8 UV sets to Mesh (when present) and the primary
    /// color set to Mesh.colors, and stores the full data here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaMeshExtraVertexData : MonoBehaviour
    {
        [Header("UV Sets (All)")]
        public string[] uvSetNames;
        public Vector2[][] uvSets;

        [Tooltip("How many UV sets were applied to Unity Mesh channels (0..7). Others exist only here.")]
        public int unityUvSetCountApplied;

        [Header("Color Sets (All)")]
        public string[] colorSetNames;
        public Color[][] colorSets;

        public void Initialize(
            string[] uvSetNames,
            Vector2[][] uvSets,
            int unityUvSetCountApplied,
            string[] colorSetNames,
            Color[][] colorSets)
        {
            this.uvSetNames = uvSetNames;
            this.uvSets = uvSets;
            this.unityUvSetCountApplied = unityUvSetCountApplied;
            this.colorSetNames = colorSetNames;
            this.colorSets = colorSets;
        }
    }
}
