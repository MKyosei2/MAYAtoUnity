// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase C-6:
    /// UnityWɊTO/Sv gConstraint/IK/Expression/Drivenh
    /// g[h ŕێ邽߂̃XibvVbgB
    ///
    /// ł g^łh OŏEĈꗗivWFNgقɋjB
    /// ڍׂ͊̊eR|[lgێĂOB
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaConstraintRetentionComponent : MonoBehaviour
    {
        [Serializable]
        public struct Item
        {
            public string typeName;
            public string objectPath;
            public string note;
        }

        [SerializeField] private Item[] items = Array.Empty<Item>();

        public IReadOnlyList<Item> Items => items;

        public static void Capture_BestEffort(GameObject root, MayaSceneData scene, MayaImportLog log)
        {
            if (root == null) return;
            log ??= new MayaImportLog();

            try
            {
                var comp = root.GetComponent<MayaConstraintRetentionComponent>();
                if (comp == null) comp = root.AddComponent<MayaConstraintRetentionComponent>();

                var found = new List<Item>(256);

                // Scan all components and keep those that look like constraints/IK/expression/etc.
                var all = root.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    var c = all[i];
                    if (c == null) continue;

                    var t = c.GetType();
                    var n = t.Name ?? "";
                    var ns = t.Namespace ?? "";

                    // Heuristics (tight enough to avoid too much noise)
                    bool ok =
                        n.Contains("Constraint", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("Ik", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("IK", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("MotionPath", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("Expression", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("Driven", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("Aim", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("Parent", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("Orient", StringComparison.OrdinalIgnoreCase) ||
                        n.Contains("Point", StringComparison.OrdinalIgnoreCase);

                    // Keep only likely Maya-related namespaces to reduce false positives
                    if (ok && ns.Contains("Maya", StringComparison.OrdinalIgnoreCase))
                    {
                        found.Add(new Item
                        {
                            typeName = t.FullName,
                            objectPath = GetPath(c.transform, root.transform),
                            note = ""
                        });
                    }
                }

                comp.items = found.ToArray();
                log.Info($"[PhaseC] ConstraintRetention captured: {comp.items.Length} components");
            }
            catch (Exception e)
            {
                log.Warn("[ConstraintRetention] Exception: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static string GetPath(Transform t, Transform root)
        {
            if (t == null) return "";
            if (root == null) return t.name;

            // Build relative path
            var stack = new List<string>(32);
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            if (cur == root) stack.Add(root.name);

            stack.Reverse();
            return string.Join("/", stack);
        }
    }
}
