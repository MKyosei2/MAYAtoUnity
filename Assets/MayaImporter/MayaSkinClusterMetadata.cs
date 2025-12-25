using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Maya skinCluster data holder (lossless).
    /// Later: can be used to build Unity SkinnedMeshRenderer.
    /// </summary>
    public sealed class MayaSkinClusterMetadata : MonoBehaviour
    {
        public string geometryNode;     // skinned shape/mesh
        public List<string> influences = new List<string>(); // joints
        public bool normalizeWeights = true;

        // Optional: store weight arrays as text for now (lossless first step)
        public string weightsBlob; // placeholder for serialized weights
    }
}
