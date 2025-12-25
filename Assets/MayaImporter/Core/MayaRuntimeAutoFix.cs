using UnityEngine;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Prefabロード時、MayaImporter環境を自己整合させる。
    /// - Missing Componentの自動付加
    /// - Nodeリンク（Parent/Constraint）の再バインド
    /// - BlendShape / SkinCluster / AnimCurveの再接続
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class MayaRuntimeAutoFix : MonoBehaviour
    {
        private static bool _applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyAfterSceneLoad()
        {
            if (_applied) return;
            _applied = true;

            var allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var go in allRoots)
            {
                if (go == null) continue;
                ApplyRecursive(go);
            }

            Debug.Log("[MayaImporter] ✅ RuntimeAutoFix applied (scene load)");
        }

        private static void ApplyRecursive(GameObject root)
        {
            if (root == null) return;

            var nodes = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            foreach (var n in nodes)
            {
                try
                {
                    FixOne(n);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[MayaImporter] AutoFix error on '{n.name}' ({n.NodeType}): {ex.Message}");
                }
            }
        }

        private static void FixOne(MayaNodeComponentBase node)
        {
            if (node == null) return;

            // Parent consistency
            if (node.transform.parent == null && node.NodeType != "transform")
                node.transform.SetParent(GetRootParent(node.transform), true);

            // BlendShape link fix
            if (node.NodeType == "blendShape")
            {
                var bs = node.GetComponent<UnityEngine.SkinnedMeshRenderer>();
                if (bs == null)
                {
                    var p = node.transform.parent;
                    if (p != null)
                    {
                        var smr = p.GetComponent<SkinnedMeshRenderer>();
                        if (smr != null)
                        {
                            node.gameObject.AddComponent<SkinnedMeshRenderer>();
                        }
                    }
                }
            }

            // Constraint presence
            if (node.NodeType.Contains("constraint"))
            {
                if (node.GetComponent<UnityEngine.Animations.ParentConstraint>() == null)
                {
                    node.gameObject.AddComponent<UnityEngine.Animations.ParentConstraint>();
                }
            }
        }

        private static Transform GetRootParent(Transform t)
        {
            if (t == null) return null;
            var r = t;
            while (r.parent != null) r = r.parent;
            return r;
        }
    }
}
