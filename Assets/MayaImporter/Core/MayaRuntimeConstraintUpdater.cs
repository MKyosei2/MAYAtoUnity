// MAYAIMPORTER_PATCH_V14: Constraint updater using Unity-version-safe object discovery
using UnityEngine;
using UnityEngine.Animations;

namespace MayaImporter.Core
{
    /// <summary>
    /// ConstraintUpdater: refreshes imported ParentConstraint components after scene load.
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class MayaRuntimeConstraintUpdater : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void UpdateConstraints()
        {
            var pcs = MayaRuntimeObjectFinder.FindAll<ParentConstraint>();
            foreach (var c in pcs)
            {
                try
                {
                    if (c == null || c.sourceCount == 0) continue;
                    c.constraintActive = true;
                    c.locked = true;
                    c.constraintActive = false;
                }
                catch { }
            }

            Debug.Log("[MayaImporter] ConstraintUpdater executed (" + pcs.Length + " ParentConstraints).");
        }
    }
}
