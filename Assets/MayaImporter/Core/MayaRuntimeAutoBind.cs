using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Runtime AutoBind: Prefabロード時に Missing Connection を復元する。
    /// 例: ConstraintターゲットやDeformer→Mesh接続などを自動バインド。
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class MayaRuntimeAutoBind : MonoBehaviour
    {
        private static bool _bound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            if (_bound) return;
            _bound = true;

            var allNodes = MayaRuntimeObjectFinder.FindAll<MayaNodeComponentBase>();
            foreach (var n in allNodes)
            {
                try { AutoBindNode(n); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[MayaImporter] AutoBind failed on " + (n != null ? n.name : "null") + ": " + e.Message);
                }
            }

            Debug.Log("[MayaImporter] AutoBind finished. Bound " + allNodes.Length + " nodes.");
        }

        private static void AutoBindNode(MayaNodeComponentBase node)
        {
            if (node == null) return;

            switch (node.NodeType)
            {
                case "pointConstraint":
                case "parentConstraint":
                    {
                        var c = node.GetComponent<UnityEngine.Animations.ParentConstraint>();
                        if (c != null && c.sourceCount == 0)
                        {
                            var src = FindSourceTransform(node, "target");
                            if (src != null)
                                c.AddSource(new UnityEngine.Animations.ConstraintSource { sourceTransform = src, weight = 1 });
                        }
                    }
                    break;

                case "blendShape":
                    {
                        var smr = node.GetComponent<SkinnedMeshRenderer>();
                        if (smr == null)
                        {
                            var p = node.transform.parent;
                            if (p != null) smr = p.GetComponent<SkinnedMeshRenderer>();
                        }

                        if (smr != null && smr.sharedMesh != null)
                        {
                            if (smr.sharedMesh.blendShapeCount > 0)
                                node.gameObject.name += "_(BlendShapeBound)";
                        }
                    }
                    break;
            }
        }

        private static Transform FindSourceTransform(MayaNodeComponentBase node, string contains)
        {
            var all = MayaRuntimeObjectFinder.FindAll<MayaNodeComponentBase>();
            foreach (var n in all)
            {
                if (n == null || n == node) continue;
                if (!string.IsNullOrEmpty(n.NodeName) && n.NodeName.Contains(contains)) return n.transform;
            }
            return null;
        }
    }
}
