using UnityEngine;
using UnityEngine.Animations;

namespace MayaImporter.Core
{
    /// <summary>
    /// ConstraintUpdater:
    /// Prefabロード後、各Constraintを一度強制更新して座標整合を取る。
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class MayaRuntimeConstraintUpdater : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void UpdateConstraints()
        {
            var pcs = GameObject.FindObjectsOfType<ParentConstraint>(true);
            foreach (var c in pcs)
            {
                try
                {
                    if (c.sourceCount == 0) continue;
                    c.constraintActive = true;
                    c.locked = true;
                    c.constraintActive = false;
                }
                catch { }
            }

            Debug.Log($"[MayaImporter] ConstraintUpdater executed ({pcs.Length} ParentConstraints).");
        }
    }
}
