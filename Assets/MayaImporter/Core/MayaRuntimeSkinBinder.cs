// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// AutoSkinBinder: Prefab[h SkinnedMeshRenderer  SkinCluster QƂ𕜋B
    /// </summary>
    [DefaultExecutionOrder(-700)]
    public sealed class MayaRuntimeSkinBinder : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindSkins()
        {
            var skins = GameObject.FindObjectsOfType<SkinnedMeshRenderer>(true);
            foreach (var s in skins)
            {
                if (s.sharedMesh == null) continue;
                if (s.bones != null && s.bones.Length > 0) continue;

                // ֍FKwtransform{[ɐݒ
                var t = s.transform.parent;
                if (t == null) continue;
                var bones = t.GetComponentsInChildren<Transform>(true);
                s.bones = bones;

                if (s.rootBone == null)
                    s.rootBone = t;

                Debug.Log($"[MayaImporter] AutoBind Skin: {s.name} bones={bones.Length}");
            }
        }
    }
}
