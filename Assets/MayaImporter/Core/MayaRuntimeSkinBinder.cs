using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// AutoSkinBinder: Prefabロード時に SkinnedMeshRenderer が SkinCluster 参照を復旧する。
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

                // 代替骨検索：同階層transformをボーンに設定
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
