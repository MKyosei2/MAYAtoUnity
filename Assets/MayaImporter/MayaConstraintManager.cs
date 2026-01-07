// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Constraints
{
    [DefaultExecutionOrder(19999)]
    public sealed class MayaConstraintManager : MonoBehaviour
    {
        private static MayaConstraintManager _instance;
        private static readonly List<MayaConstraintDriver> _drivers = new List<MayaConstraintDriver>();
        private static bool _dirtySort = true;

        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = GameObject.Find("MayaConstraintManager");
            if (go == null) go = new GameObject("MayaConstraintManager");

            _instance = go.GetComponent<MayaConstraintManager>();
            if (_instance == null) _instance = go.AddComponent<MayaConstraintManager>();

            DontDestroyOnLoad(go);
        }

        public static void Register(MayaConstraintDriver d)
        {
            if (d == null) return;
            if (_drivers.Contains(d)) return;
            _drivers.Add(d);
            _dirtySort = true;
        }

        public static void Unregister(MayaConstraintDriver d)
        {
            if (d == null) return;
            if (_drivers.Remove(d)) _dirtySort = true;
        }

        /// <summary>
        /// Evaluate all constraints immediately (for editor tools / bake / scrubbing).
        /// Maya time is handled elsewhere; constraints just read current transforms.
        /// </summary>
        public static void EvaluateNow()
        {
            EnsureExists();
            _instance.EvaluateNowImpl();
        }

        /// <summary>
        /// Backward-compatible overload.
        /// Some editor scripts call EvaluateNow(time). We don't need time here,
        /// but we keep the signature so compilation succeeds.
        /// </summary>
        public static void EvaluateNow(float _ignoredTime)
        {
            EvaluateNow();
        }

        private void LateUpdate()
        {
            EvaluateNowImpl();
        }

        private void EvaluateNowImpl()
        {
            if (_dirtySort)
            {
                _drivers.Sort((a, b) => (a?.Priority ?? 0).CompareTo(b?.Priority ?? 0));
                _dirtySort = false;
            }

            for (int i = 0; i < _drivers.Count; i++)
            {
                var d = _drivers[i];
                if (d == null) continue;
                d.ApplyConstraintInternal();
            }
        }
    }
}
