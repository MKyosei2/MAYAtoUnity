// Assets/MayaImporter/MayaProceduralTextureMetadata.cs
// Procedural texture bake/debug metadata (Maya不要運用用の共通メタ)

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

        // 互換: 古いコードが width/height を使う場合
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

        // 互換: remap系などが入力ノード名を別名で持っている場合
        public string inputNodeA;
        public string inputNodeB;
        public string inputNodeC;

        [Header("UV (best-effort)")]
        public Vector2 repeatUV = Vector2.one;
        public Vector2 offsetUV = Vector2.zero;
        public float rotateUVDegrees = 0f;

        [Header("Bake Output")]
        public string bakedTexturePath;

        // 互換: remap系などが bakedPngPath を参照する場合
        public string bakedPngPath
        {
            get => bakedTexturePath;
            set => bakedTexturePath = value;
        }

        [TextArea]
        public string notes;
    }
}
