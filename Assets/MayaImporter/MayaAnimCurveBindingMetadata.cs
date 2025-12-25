// Assets/MayaImporter/MayaAnimCurveBindingMetadata.cs
// Phase-1: animCurve が “どこに刺さっているか” を Unity 側に保持するためのメタ。
// MayaAnimationManager は SceneData.Connections を直接読むので必須ではないが、
// 「再構築/Clip化/デバッグ」に必須級の情報。

using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Animation
{
    [DisallowMultipleComponent]
    public sealed class MayaAnimCurveBindingMetadata : MonoBehaviour
    {
        [Header("Identity")]
        public string nodeName;
        public string nodeType;

        [Header("DrivenKey")]
        public bool isDriven;

        [Header("Outgoing destination plugs (raw)")]
        public List<string> dstPlugs = new List<string>();

        [Header("Outgoing destination node.attr (best-effort)")]
        public List<string> dstNodeAttrs = new List<string>();

        public void Clear()
        {
            nodeName = null;
            nodeType = null;
            isDriven = false;
            dstPlugs.Clear();
            dstNodeAttrs.Clear();
        }
    }
}
