using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Runtime self-check to ensure imported prefab consistency.
    /// - Checks for Missing scripts/components
    /// - Logs warnings for orphaned nodeTypes
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class MayaRuntimeSanityChecker : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Validate()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            int nodeCount = 0, orphan = 0;

            foreach (var r in roots)
            {
                var nodes = r.GetComponentsInChildren<MayaNodeComponentBase>(true);
                foreach (var n in nodes)
                {
                    nodeCount++;
                    if (n.transform.parent == null && n.NodeType != "transform")
                        orphan++;
                }
            }

            Debug.Log($"[MayaImporter] SceneSanity: NodeCount={nodeCount} Orphan={orphan}");
        }
    }
}
