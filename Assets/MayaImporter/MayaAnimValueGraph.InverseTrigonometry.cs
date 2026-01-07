// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// Assets/MayaImporter/MayaAnimValueGraph.InverseTrigonometry.cs
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    public sealed partial class MayaAnimValueGraph
    {
        /// <summary>
        /// asin/acos/atan/atan2 (and *DL variants) as GraphCompute nodes.
        /// Output angle is treated as degrees.
        /// </summary>
        private bool TryEvaluateInverseTrigonometry(MayaNodeComponentBase node, string attrPath, float frame, out float value)
        {
            value = 0f;
            if (node == null) return false;

            var nt = node.NodeType ?? string.Empty;

            bool isInv =
                nt == "asin" || nt == "asinDL" ||
                nt == "acos" || nt == "acosDL" ||
                nt == "atan" || nt == "atanDL" ||
                nt == "atan2" || nt == "atan2DL";

            if (!isInv) return false;

            if (!LooksLikeOutputAttr(attrPath))
                return false;

            // inputs are unitless doubles; outputs are angle (deg) for these nodes
            switch (nt)
            {
                case "asin":
                case "asinDL":
                    {
                        float x = GetInputValue(node, frame, "input", "in", "input1", "i1", "x");
                        x = Mathf.Clamp(x, -1f, 1f); // avoid NaN
                        value = Mathf.Asin(x) * Mathf.Rad2Deg;
                        return true;
                    }

                case "acos":
                case "acosDL":
                    {
                        float x = GetInputValue(node, frame, "input", "in", "input1", "i1", "x");
                        x = Mathf.Clamp(x, -1f, 1f);
                        value = Mathf.Acos(x) * Mathf.Rad2Deg;
                        return true;
                    }

                case "atan":
                case "atanDL":
                    {
                        float x = GetInputValue(node, frame, "input", "in", "input1", "i1", "x");
                        value = Mathf.Atan(x) * Mathf.Rad2Deg;
                        return true;
                    }

                case "atan2":
                case "atan2DL":
                    {
                        // Maya naming varies; best-effort accept:
                        // - input1/input2
                        // - y/x
                        float y = GetInputValue(node, frame, "input1", "i1", "y");
                        float x = GetInputValue(node, frame, "input2", "i2", "x");
                        value = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                        return true;
                    }
            }

            return false;
        }
    }
}
