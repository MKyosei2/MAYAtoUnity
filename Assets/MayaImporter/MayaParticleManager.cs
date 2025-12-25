using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Dynamics
{
    [DefaultExecutionOrder(22500)]
    [DisallowMultipleComponent]
    public sealed class MayaParticleManager : MonoBehaviour
    {
        private static MayaParticleManager _instance;

        private static readonly List<MayaNParticleRuntimeSystem> _systems = new List<MayaNParticleRuntimeSystem>(128);
        private static bool _dirtySort = true;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = GameObject.Find("[MayaParticleManager]");
            if (go == null) go = new GameObject("[MayaParticleManager]");

            _instance = go.GetComponent<MayaParticleManager>();
            if (_instance == null) _instance = go.AddComponent<MayaParticleManager>();

            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }

        public static void Register(MayaNParticleRuntimeSystem s)
        {
            if (s == null) return;
            if (!_systems.Contains(s))
            {
                _systems.Add(s);
                _dirtySort = true;
            }
        }

        public static void Unregister(MayaNParticleRuntimeSystem s)
        {
            if (s == null) return;
            if (_systems.Remove(s)) _dirtySort = true;
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
            EvaluateNow(Time.deltaTime);
        }

        public static void EvaluateNow(float dt)
        {
            if (_systems.Count == 0) return;

            if (_dirtySort)
            {
                _systems.Sort((a, b) =>
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

            for (int i = 0; i < _systems.Count; i++)
            {
                var s = _systems[i];
                if (s == null || !s.isActiveAndEnabled) continue;
                s.ApplyForcesInternal(dt);
            }
        }
    }
}
