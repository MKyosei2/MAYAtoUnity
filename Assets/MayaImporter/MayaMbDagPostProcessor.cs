// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Step9:
    /// - For .mb DAG-path nodes, attach shortName (leaf) metadata.
    /// - If a node is a shape (ends with "Shape"), treat it as mesh-like shape:
    ///   - Ensure NodeType at least "mesh" (unless already something else).
    ///   - Add linkage attributes:
    ///       parentTransform: .mbShapeChild = fullShapeName
    ///       shapeNode:       .mbParentTransform = fullParentName
    /// - Do NOT delete or override Raw. This is additive and safe.
    /// </summary>
    public static class MayaMbDagPostProcessor
    {
        public static void Apply(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null) return;
            if (scene.SourceKind != MayaSourceKind.BinaryMb) return;
            if (scene.Nodes == null || scene.Nodes.Count == 0) return;

            int shortNameTagged = 0;
            int shapeLinked = 0;
            int meshTyped = 0;

            foreach (var kv in scene.Nodes)
            {
                var rec = kv.Value;
                if (rec == null) continue;

                // Tag shortName for any dag-like name
                var leaf = LeafOfDag(rec.Name);
                if (!string.IsNullOrEmpty(leaf))
                {
                    if (!rec.Attributes.ContainsKey(".shortName"))
                    {
                        rec.Attributes[".shortName"] = new RawAttributeValue("string", new List<string> { leaf });
                        shortNameTagged++;
                    }
                }

                // Shape detection
                if (!string.IsNullOrEmpty(leaf) && leaf.EndsWith("Shape", StringComparison.Ordinal))
                {
                    // Ensure nodeType at least mesh if placeholder-ish
                    if (string.IsNullOrEmpty(rec.NodeType) || rec.NodeType == "unknown" || rec.NodeType == "transform")
                    {
                        rec.NodeType = "mesh";
                        meshTyped++;
                    }

                    // Link to parent transform if present
                    if (!string.IsNullOrEmpty(rec.ParentName) && scene.Nodes.TryGetValue(rec.ParentName, out var parent) && parent != null)
                    {
                        // parent -> shape
                        if (!parent.Attributes.ContainsKey(".mbShapeChild"))
                        {
                            parent.Attributes[".mbShapeChild"] = new RawAttributeValue("string", new List<string> { rec.Name });
                            shapeLinked++;
                        }
                        else
                        {
                            // If already set, keep first, but also store list-like hint (non-destructive)
                            if (!parent.Attributes.ContainsKey(".mbShapeChild2"))
                                parent.Attributes[".mbShapeChild2"] = new RawAttributeValue("string", new List<string> { rec.Name });
                        }

                        // shape -> parent
                        if (!rec.Attributes.ContainsKey(".mbParentTransform"))
                        {
                            rec.Attributes[".mbParentTransform"] = new RawAttributeValue("string", new List<string> { parent.Name });
                            shapeLinked++;
                        }
                    }
                }
            }

            log?.Info($".mb post: shortNameTagged={shortNameTagged}, meshTyped={meshTyped}, shapeLinked={shapeLinked} (Stage: DAG/Shape Link).");
        }

        private static string LeafOfDag(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            int last = name.LastIndexOf('|');
            if (last >= 0 && last < name.Length - 1) return name.Substring(last + 1);
            // If it's not a dag path, still allow leaf = name
            return name;
        }
    }
}
