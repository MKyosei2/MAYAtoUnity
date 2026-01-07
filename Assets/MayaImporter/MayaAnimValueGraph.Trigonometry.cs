// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// Assets/MayaImporter/MayaAnimValueGraph.Trigonometry.cs
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    public sealed partial class MayaAnimValueGraph
    {
        /// <summary>
        /// sin/cos/tan (and *DL variants) as GraphCompute nodes.
        /// Angle is treated as degrees (matches Maya rotate channels and common DG usage).
        /// </summary>
        private bool TryEvaluateTrigonometry(MayaNodeComponentBase node, string attrPath, float frame, out float value)
        {
            value = 0f;
            if (node == null) return false;

            var nt = node.NodeType ?? string.Empty;

            bool isTrig =
                nt == "sin" || nt == "sinDL" ||
                nt == "cos" || nt == "cosDL" ||
                nt == "tan" || nt == "tanDL";

            if (!isTrig) return false;

            // Only compute for output-ish attrs; for others fall back to local reading.
            if (!LooksLikeOutputAttr(attrPath))
                return false;

            // Common attrs:
            // - input / in (many DG nodes)
            // - input1 (some DL nodes)
            // - angle / a (best-effort)
            float deg = GetInputValue(node, frame, "input", "in", "angle", "a", "input1", "i1", "x");

            float rad = deg * Mathf.Deg2Rad;

            switch (nt)
            {
                case "sin":
                case "sinDL":
                    value = Mathf.Sin(rad);
                    return true;

                case "cos":
                case "cosDL":
                    value = Mathf.Cos(rad);
                    return true;

                case "tan":
                case "tanDL":
                    value = Mathf.Tan(rad);
                    return true;
            }

            return false;
        }
    }
}
