using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// UnityのMaterialだけでは保持しきれない「100%データ」用の保持先。
    /// - UV回転（Unity標準のSetTextureScale/Offsetでは表現できない）
    /// - RoughnessMap等、UnityのMetallicGlossMapへ未反映/未パックの情報
    /// - 実際にロードしたTexture参照（ランタイム/エディタどちらでも確認可能）
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaMaterialReconstructionExtras : MonoBehaviour
    {
        [Header("Nodes")]
        public string baseColorNode;
        public string normalNode;
        public string metallicNode;
        public string roughnessNode;
        public string emissionNode;

        [Header("Loaded Textures (runtime refs)")]
        public Texture2D baseColorTexture;
        public Texture2D normalTexture;
        public Texture2D metallicTexture;
        public Texture2D roughnessTexture;
        public Texture2D emissionTexture;

        [Header("Generated / Packed")]
        public Texture2D packedMetallicSmoothness;
        public string packedNote;

        [Header("Normal")]
        public float normalScale = 1f;

        [Header("Transparency")]
        public bool isTransparent;

        [Serializable]
        public struct UvSlot
        {
            public string slotName;        // "BaseColor", "Normal"...
            public Vector2 repeat;
            public Vector2 offset;
            public float rotateDegrees;    // Unity標準には無いので保持のみ
        }

        [Header("UV slots (Unity lacks rotation)")]
        public List<UvSlot> uvSlots = new List<UvSlot>();

        public void ResetRuntime()
        {
            baseColorTexture = null;
            normalTexture = null;
            metallicTexture = null;
            roughnessTexture = null;
            emissionTexture = null;

            packedMetallicSmoothness = null;
            packedNote = null;

            normalScale = 1f;
            isTransparent = false;

            if (uvSlots == null) uvSlots = new List<UvSlot>();
            uvSlots.Clear();
        }

        public void AddUvSlot(string slotName, Vector2 repeat, Vector2 offset, float rotateDegrees)
        {
            if (uvSlots == null) uvSlots = new List<UvSlot>();

            uvSlots.Add(new UvSlot
            {
                slotName = slotName,
                repeat = repeat,
                offset = offset,
                rotateDegrees = rotateDegrees
            });
        }
    }
}
