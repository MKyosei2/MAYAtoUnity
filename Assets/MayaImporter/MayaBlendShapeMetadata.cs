using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Maya blendShape data holder (Unity-only runtime friendly).
    /// </summary>
    public sealed class MayaBlendShapeMetadata : MonoBehaviour
    {
        [System.Serializable]
        public struct Target
        {
            /// <summary>BlendShape group index (weight array index).</summary>
            public int targetIndex;

            /// <summary>Maya node name (best effort). Typically a mesh shape/transform leaf.</summary>
            public string targetMesh;

            /// <summary>
            /// ✅ Unity側で実際に作成した BlendShape 名（確定名）。
            /// 例: MAYA_BS_003_headSmile
            /// </summary>
            public string name;

            /// <summary>Maya weight (commonly 0..1). Unity uses 0..100 for blendshape weight.</summary>
            public float weight;
        }

        public List<Target> targets = new List<Target>();
    }
}
