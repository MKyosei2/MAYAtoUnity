using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Constraints
{
    /// <summary>
    /// Evaluates "surface/mesh" constraints (geometry/normal/tangent/pointOnPoly)
    /// in a deterministic LateUpdate order.
    /// </summary>
    [DefaultExecutionOrder(20950)] // motionPath(20500) -> surface constraints(20950) -> constraints(21000) -> IK(22000)
    [DisallowMultipleComponent]
    public sealed class MayaSurfaceConstraintManager : MonoBehaviour
    {
        private static MayaSurfaceConstraintManager _instance;

        private static readonly List<MayaSurfaceConstraintDriver> _drivers = new List<MayaSurfaceConstraintDriver>(256);
        private static bool _dirtySort = true;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = GameObject.Find("[MayaSurfaceConstraintManager]");
            if (go == null) go = new GameObject("[MayaSurfaceConstraintManager]");

            _instance = go.GetComponent<MayaSurfaceConstraintManager>();
            if (_instance == null) _instance = go.AddComponent<MayaSurfaceConstraintManager>();

            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
        }

        public static void Register(MayaSurfaceConstraintDriver d)
        {
            if (d == null) return;
            if (!_drivers.Contains(d))
            {
                _drivers.Add(d);
                _dirtySort = true;
            }
        }

        public static void Unregister(MayaSurfaceConstraintDriver d)
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

                    // shallower first = more stable
                    int da = GetDepth(a.Constrained);
                    int db = GetDepth(b.Constrained);
                    int d = da.CompareTo(db);
                    if (d != 0) return d;

                    return a.GetInstanceID().CompareTo(b.GetInstanceID());
                });
                _dirtySort = false;
            }

            for (int i = 0; i < _drivers.Count; i++)
            {
                var d = _drivers[i];
                if (d == null || !d.isActiveAndEnabled) continue;
                d.ApplyConstraintInternal();
            }
        }

        private static int GetDepth(Transform t)
        {
            int depth = 0;
            while (t != null)
            {
                depth++;
                t = t.parent;
                if (depth > 512) break;
            }
            return depth;
        }
    }
}
