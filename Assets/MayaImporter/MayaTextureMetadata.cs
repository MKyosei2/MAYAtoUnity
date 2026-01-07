// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Maya texture (file / place2dTexture etc.) metadata holder.
    /// UnityɖTOڑ܂߂ĕێi100_jB
    /// </summary>
    public sealed class MayaTextureMetadata : MonoBehaviour
    {
        // file node
        public string fileTextureName;   // Maya: fileTextureName
        public bool ignoreColorSpaceFileRules;
        public string colorSpace;        // optional

        // place2dTexture (subset)
        public Vector2 repeatUV = Vector2.one;
        public Vector2 offsetUV = Vector2.zero;
        public float rotateUVDegrees = 0f;

        // connections (best effort)
        public string connectedPlace2dNodeName;
    }
}
