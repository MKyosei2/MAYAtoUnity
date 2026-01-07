// Assets/MayaImporter/MayaConstraintDynamicsGizmos.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MayaImporter.Portfolio
{
    /// <summary>
    /// Gizmo visualizer for portfolio demo:
    /// - Finds components whose type name contains "Constraint" or "Dynamics"/"Dynamic"
    /// - Draws lines from owner to referenced transforms (best-effort, reflection)
    ///
    /// This is purely for visualization (does not change simulation).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaConstraintDynamicsGizmos : MonoBehaviour
    {
        public bool showConstraints = true;
        public bool showDynamics = true;

        [Range(0, 5000)] public int maxLines = 500;

        [Tooltip("If true, lines only draw when the hierarchy root is selected.")]
        public bool drawOnlyWhenSelected = true;

        private void OnDrawGizmos()
        {
            if (drawOnlyWhenSelected) return;
            Draw();
        }

        private void OnDrawGizmosSelected()
        {
            Draw();
        }

        private void Draw()
        {
            int lines = 0;

            var behaviours = GetComponentsInChildren<Behaviour>(true);
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                var tn = b.GetType().Name;

                bool isConstraint = showConstraints && tn.IndexOf("Constraint", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isDynamics = showDynamics &&
                                  (tn.IndexOf("Dynamics", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   tn.IndexOf("Dynamic", StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isConstraint && !isDynamics) continue;

                var owner = b.transform;
                var refs = ExtractReferencedTransforms(b);

                Gizmos.color = isConstraint ? Color.cyan : Color.magenta;

                foreach (var t in refs)
                {
                    if (t == null || t == owner) continue;
                    Gizmos.DrawLine(owner.position, t.position);
                    lines++;
                    if (lines >= maxLines) return;
                }
            }
        }

        private static List<Transform> ExtractReferencedTransforms(Component c)
        {
            var list = new List<Transform>(8);
            if (c == null) return list;

            var t = c.GetType();

            // Fields
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                object v = null;
                try { v = fields[i].GetValue(c); } catch { }
                AddTransformish(list, v);
            }

            // Properties (safe subset)
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < props.Length; i++)
            {
                if (!props[i].CanRead) continue;
                if (props[i].GetIndexParameters().Length != 0) continue;
                object v = null;
                try { v = props[i].GetValue(c, null); } catch { }
                AddTransformish(list, v);
            }

            // De-dup
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null) { list.RemoveAt(i); continue; }
                for (int j = 0; j < i; j++)
                {
                    if (list[j] == list[i]) { list.RemoveAt(i); break; }
                }
            }

            return list;
        }

        private static void AddTransformish(List<Transform> list, object v)
        {
            if (v == null) return;

            if (v is Transform tr) { list.Add(tr); return; }
            if (v is GameObject go) { list.Add(go.transform); return; }
            if (v is Component c) { list.Add(c.transform); return; }

            if (v is System.Collections.IEnumerable en && !(v is string))
            {
                int count = 0;
                foreach (var it in en)
                {
                    if (it == null) continue;
                    if (it is Transform t) list.Add(t);
                    else if (it is GameObject g) list.Add(g.transform);
                    else if (it is Component cc) list.Add(cc.transform);
                    count++;
                    if (count > 64) break; // safety
                }
            }
        }
    }
}
