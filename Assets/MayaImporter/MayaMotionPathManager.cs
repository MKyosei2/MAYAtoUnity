using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Animation
{
    [DefaultExecutionOrder(20500)]
    [DisallowMultipleComponent]
    public sealed class MayaMotionPathManager : MonoBehaviour
    {
        private static MayaMotionPathManager _instance;

        private static readonly List<MayaMotionPathDriver> _drivers = new List<MayaMotionPathDriver>(128);
        private static bool _dirtySort = true;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = GameObject.Find("[MayaMotionPathManager]");
            if (go == null) go = new GameObject("[MayaMotionPathManager]");

            _instance = go.GetComponent<MayaMotionPathManager>();
            if (_instance == null) _instance = go.AddComponent<MayaMotionPathManager>();

            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }

        public static void Register(MayaMotionPathDriver d)
        {
            if (d == null) return;
            if (!_drivers.Contains(d))
            {
                _drivers.Add(d);
                _dirtySort = true;
            }
        }

        public static void Unregister(MayaMotionPathDriver d)
        {
            if (d == null) return;
            if (_drivers.Remove(d))
                _dirtySort = true;
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

        private void LateUpdate()
        {
            EvaluateNow();
        }

        public static void EvaluateNow()
        {
            if (_drivers.Count == 0) return;

            if (_dirtySort)
            {
                _drivers.Sort((a, b) =>
                {
                    if (ReferenceEquals(a, b)) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;
                    int p = a.Priority.CompareTo(b.Priority);
                    if (p != 0) return p;
                    return a.GetInstanceID().CompareTo(b.GetInstanceID());
                });
                _dirtySort = false;
            }

            for (int i = 0; i < _drivers.Count; i++)
            {
                var d = _drivers[i];
                if (d == null || !d.isActiveAndEnabled) continue;
                d.ApplyInternal();
            }
        }
    }
}
