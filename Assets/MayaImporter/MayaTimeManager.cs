using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Advances MayaTimeState using Unity time.
    /// Put one in scene automatically.
    /// </summary>
    [DefaultExecutionOrder(18000)]
    [DisallowMultipleComponent]
    public sealed class MayaTimeManager : MonoBehaviour
    {
        private static MayaTimeManager _instance;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = GameObject.Find("[MayaTimeManager]");
            if (go == null) go = new GameObject("[MayaTimeManager]");

            _instance = go.GetComponent<MayaTimeManager>();
            if (_instance == null) _instance = go.AddComponent<MayaTimeManager>();

            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }
            _instance = this;
        }

        private void Update()
        {
            if (!MayaTimeState.Playing) return;

            float fps = Mathf.Max(1e-6f, MayaTimeState.Fps);
            MayaTimeState.CurrentFrame += Time.deltaTime * fps * MayaTimeState.Speed;

            if (!MayaTimeState.Loop) return;

            float start = MayaTimeState.StartFrame;
            float end = Mathf.Max(MayaTimeState.EndFrame, start + 1f);

            if (MayaTimeState.CurrentFrame > end)
                MayaTimeState.CurrentFrame = start + (MayaTimeState.CurrentFrame - end);
            else if (MayaTimeState.CurrentFrame < start)
                MayaTimeState.CurrentFrame = end - (start - MayaTimeState.CurrentFrame);
        }
    }
}
