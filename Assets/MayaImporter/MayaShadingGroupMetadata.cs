using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Components
{
    /// <summary>
    /// Maya shadingEngine(set) metadata holder.
    /// - surfaceShader: どのマテリアルノードが割り当たっているか
    /// - members: どのDAG(Shape/Transform)に適用されるか
    /// </summary>
    public sealed class MayaShadingGroupMetadata : MonoBehaviour
    {
        public string surfaceShaderNodeName;

        [System.Serializable]
        public struct Member
        {
            public string nodeName;      // e.g. "|grp|meshShape" or "meshShape"
            public string componentSpec; // e.g. ".f[0:10]" (保持用)
        }

        public List<Member> members = new List<Member>();
    }
}
