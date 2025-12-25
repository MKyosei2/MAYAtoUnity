using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.IK
{
    [DefaultExecutionOrder(22000)]
    [DisallowMultipleComponent]
    public sealed class MayaIkManager : MonoBehaviour
    {
        private static MayaIkManager _instance;

        private static readonly List<MayaIkRuntimeSolver> _solvers = new List<MayaIkRuntimeSolver>(128);
        private static bool _dirtySort = true;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = GameObject.Find("[MayaIkManager]");
            if (go == null) go = new GameObject("[MayaIkManager]");

            _instance = go.GetComponent<MayaIkManager>();
            if (_instance == null) _instance = go.AddComponent<MayaIkManager>();

            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }

        public static void Register(MayaIkRuntimeSolver s)
        {
            if (s == null) return;
            if (!_solvers.Contains(s))
            {
                _solvers.Add(s);
                _dirtySort = true;
            }
        }

        public static void Unregister(MayaIkRuntimeSolver s)
        {
            if (s == null) return;
            if (_solvers.Remove(s))
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
            if (_solvers.Count == 0) return;

            if (_dirtySort)
            {
                _solvers.Sort((a, b) =>
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

            for (int i = 0; i < _solvers.Count; i++)
            {
                var s = _solvers[i];
                if (s == null || !s.isActiveAndEnabled) continue;
                s.SolveInternal();
            }
        }
    }
}
