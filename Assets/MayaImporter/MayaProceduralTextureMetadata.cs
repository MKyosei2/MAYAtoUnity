// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// Assets/MayaImporter/MayaProceduralTextureMetadata.cs
// Procedural texture bake/debug metadata (Mayasv^pp̋ʃ^)

using UnityEngine;

namespace MayaImporter.Components
{
    [DisallowMultipleComponent]
    public sealed class MayaProceduralTextureMetadata : MonoBehaviour
    {
        [Header("Identity")]
        public string mayaNodeType;
        public string mayaNodeName;
        public string mayaNodeUUID;

        [Header("Bake Size")]
        public int bakedWidth = 256;
        public int bakedHeight = 256;

        // ݊: ÂR[h width/height gꍇ
        public int width
        {
            get => bakedWidth;
            set => bakedWidth = value;
        }

        public int height
        {
            get => bakedHeight;
            set => bakedHeight = value;
        }

        [Header("Connections (best-effort)")]
        public string connectedPlace2dNodeName;
        public string inputTextureNodeName;

        // ݊: remapnȂǂ̓m[hʖŎĂꍇ
        public string inputNodeA;
        public string inputNodeB;
        public string inputNodeC;

        [Header("UV (best-effort)")]
        public Vector2 repeatUV = Vector2.one;
        public Vector2 offsetUV = Vector2.zero;
        public float rotateUVDegrees = 0f;

        [Header("Bake Output")]
        public string bakedTexturePath;

        // ݊: remapnȂǂ bakedPngPath QƂꍇ
        public string bakedPngPath
        {
            get => bakedTexturePath;
            set => bakedTexturePath = value;
        }

        [TextArea]
        public string notes;
    }
}
