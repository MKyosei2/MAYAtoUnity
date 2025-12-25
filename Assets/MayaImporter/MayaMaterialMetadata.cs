using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Maya material/shader node metadata holder.
    /// Unity MaterialÇ÷ïœä∑Ç∑ÇÈÇΩÇﬂÇÃç≈í·å¿Å{losslessï€éùÅB
    /// </summary>
    public sealed class MayaMaterialMetadata : MonoBehaviour
    {
        public string mayaShaderType; // lambert/phong/blinn/aiStandardSurface...

        public Color baseColor = Color.gray;
        public float baseWeight = 1f;

        public float metallic = 0f;
        public float roughness = 0.5f;
        public float smoothness = 0.5f;

        public Color emissionColor = Color.black;

        public float opacity = 1f; // 1=opaque, 0=transparent

        // texture node names (best effort)
        public string baseColorTextureNode;
        public string metallicTextureNode;
        public string roughnessTextureNode;
        public string normalTextureNode;
        public string emissionTextureNode;
    }
}
