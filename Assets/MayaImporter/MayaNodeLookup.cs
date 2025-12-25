using System;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Robust lookup: Maya node name (full DAG path/namespace) -> Unity Transform/GameObject.
    /// UnitySceneBuilder names GameObjects by leaf name, so we search by:
    /// 1) exact MayaNodeComponentBase.NodeName match
    /// 2) leaf-name match
    /// 3) fallback: GameObject.Find(leaf)
    /// </summary>
    public static class MayaNodeLookup
    {
        public static Transform FindTransform(string mayaNodeNameOrDag)
        {
            if (string.IsNullOrEmpty(mayaNodeNameOrDag)) return null;

            // 1) Exact match by stored NodeName (best)
            var allNodes = Resources.FindObjectsOfTypeAll<MayaNodeComponentBase>();
            for (int i = 0; i < allNodes.Length; i++)
            {
                var n = allNodes[i];
                if (n == null) continue;
                if (!n.gameObject.scene.IsValid()) continue;

                if (MayaPlugUtil.NodeMatches(n.NodeName, mayaNodeNameOrDag))
                    return n.transform;
            }

            // 2) Leaf match among transforms (good fallback)
            var leaf = MayaPlugUtil.LeafName(mayaNodeNameOrDag);

#if UNITY_2023_1_OR_NEWER
            var allTr = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTr.Length; i++)
            {
                var t = allTr[i];
                if (t == null) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                if (string.Equals(t.name, leaf, StringComparison.Ordinal))
                    return t;
            }
#else
            var allTr = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < allTr.Length; i++)
            {
                var t = allTr[i];
                if (t == null) continue;
                if (!t.gameObject.scene.IsValid()) continue;
                if (string.Equals(t.name, leaf, StringComparison.Ordinal))
                    return t;
            }
#endif

            // 3) Final fallback: GameObject.Find(leaf)
            var go = GameObject.Find(leaf);
            return go != null ? go.transform : null;
        }
    }
}
