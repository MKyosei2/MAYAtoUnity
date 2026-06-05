// MAYAIMPORTER_PATCH_V14: Skin binder using Unity-version-safe object discovery
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// AutoSkinBinder: assigns fallback bones for imported SkinnedMeshRenderers that have no bones.
    /// </summary>
    [DefaultExecutionOrder(-700)]
    public sealed class MayaRuntimeSkinBinder : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindSkins()
        {
            var skins = MayaRuntimeObjectFinder.FindAll<SkinnedMeshRenderer>();
            foreach (var s in skins)
            {
                if (s == null) continue;
                if (s.sharedMesh == null) continue;
                if (s.bones != null && s.bones.Length > 0) continue;

                var t = s.transform.parent;
                if (t == null) continue;
                var bones = t.GetComponentsInChildren<Transform>(true);
                s.bones = bones;

                if (s.rootBone == null) s.rootBone = t;

                Debug.Log("[MayaImporter] AutoBind Skin: " + s.name + " bones=" + bones.Length);
            }
        }
    }
}
