using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya BlendShape Weight Node
    /// Represents a single blendShape target weight.
    ///
    /// Gate0:
    /// - Must inherit MayaNodeComponentBase when it has [MayaNodeType]
    ///   so NodeFactory can register it and the project stays stable.
    /// </summary>
    [DisallowMultipleComponent]
    [MayaNodeType("blendShapeWeight")]
    public sealed class BlendShapeWeightNode : MayaNodeComponentBase
    {
        [Header("Target Info")]

        [Tooltip("Target shape name (best-effort; may remain empty if not inferable from data)")]
        public string targetName;

        [Tooltip("Target index (best-effort; inferred from connections to blendShape weights)")]
        public int targetIndex = -1;

        [Tooltip("Weight value (normalized 0..1)")]
        [Range(0f, 1f)]
        public float weight = 0f;

        [Header("Inbetween")]

        [Tooltip("Is inbetween target (best-effort)")]
        public bool isInbetween = false;

        [Tooltip("Inbetween weight position (best-effort)")]
        public float inbetweenWeight = 0f;

        // ===== Initialization (optional external helper) =====

        /// <summary>
        /// Initialize blendShape weight data explicitly (optional).
        /// Raw Maya attributes and connections are still stored losslessly in the base class.
        /// </summary>
        public void InitializeBlendShapeWeight(
            string name,
            int index,
            float weightValue,
            bool inbetween,
            float inbetweenPos)
        {
            targetName = name;
            targetIndex = index;
            weight = Mathf.Clamp01(weightValue);
            isInbetween = inbetween;
            inbetweenWeight = inbetweenPos;
        }

        // ===== Phase-1 Step-3 =====

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // This nodefs main job in Phase-1 is:
            // 1) exist as "1 Maya node => 1 Unity component"
            // 2) keep raw attrs/connections losslessly (handled by base)
            // 3) provide best-effort convenience fields for downstream systems

            // Best-effort: infer targetIndex from outgoing connection to blendShape weight arrays.
            if (targetIndex < 0)
            {
                var inferred = InferTargetIndexFromConnections();
                if (inferred >= 0) targetIndex = inferred;
            }

            // Best-effort: read weight from typical attribute keys if present.
            // (Exact key differs depending on exporter; raw tokens remain available anyway.)
            if (TryReadFloatFromAnyAttr(out var w,
                    ".weight", "weight", ".w", "w", ".value", "value"))
            {
                // Many Maya weight-like attrs are 0..1, but we clamp for safety.
                weight = Mathf.Clamp01(w);
            }

            // Best-effort: inbetween flags/position (if present).
            if (TryReadBoolFromAnyAttr(out var ib,
                    ".isInbetween", "isInbetween", ".inbetween", "inbetween"))
            {
                isInbetween = ib;
            }

            if (TryReadFloatFromAnyAttr(out var ibw,
                    ".inbetweenWeight", "inbetweenWeight", ".inbetween", "inbetween"))
            {
                inbetweenWeight = ibw;
            }

            // Best-effort: derive a human-friendly name if none set.
            if (string.IsNullOrEmpty(targetName))
            {
                var leaf = GetLeaf(NodeName);
                if (!string.IsNullOrEmpty(leaf))
                    targetName = leaf;
            }

            // Keep logging minimal; this node can be numerous.
            // log.Info($"[blendShapeWeight] {NodeName} idx={targetIndex} w={weight}");
        }

        // ===== Internals =====

        private int InferTargetIndexFromConnections()
        {
            if (Connections == null || Connections.Count == 0)
                return -1;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                // For this node, we want where THIS node is the source (drives others).
                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                // The index is usually on the destination plug: blendShape#.w[IDX] / .weight[IDX]
                if (TryParseIndexFromPlug(c.DstPlug, out var idx))
                    return idx;
            }

            // Fallback: try source plug too (rare, but harmless)
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (TryParseIndexFromPlug(c.SrcPlug, out var idx))
                    return idx;
            }

            // Last fallback: if node name contains [n]
            var leafName = GetLeaf(NodeName);
            if (TryParseIndexFromBracket(leafName, out var nameIdx))
                return nameIdx;

            return -1;
        }

        private static bool TryParseIndexFromPlug(string plug, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(plug))
                return false;

            // common patterns:
            //   "... .w[3]" , "... .weight[3]"
            // We'll just grab the last [...] integer if any.
            return TryParseIndexFromBracket(plug, out index);
        }

        private static bool TryParseIndexFromBracket(string s, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(s)) return false;

            int r = s.LastIndexOf(']');
            if (r <= 0) return false;

            int l = s.LastIndexOf('[', r);
            if (l < 0 || l + 1 >= r) return false;

            var inner = s.Substring(l + 1, r - l - 1);
            return int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
        }

        private bool TryReadFloatFromAnyAttr(out float value, params string[] keys)
        {
            value = 0f;
            if (keys == null || keys.Length == 0) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a == null || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return true;
            }

            return false;
        }

        private bool TryReadBoolFromAnyAttr(out bool value, params string[] keys)
        {
            value = false;
            if (keys == null || keys.Length == 0) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!TryGetAttr(keys[i], out var a) || a == null || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (TryParseBoolToken(a.Tokens[0], out value))
                    return true;
            }

            return false;
        }

        private static bool TryParseBoolToken(string token, out bool value)
        {
            value = false;
            if (string.IsNullOrEmpty(token)) return false;

            // Maya-ish / generic tokens
            if (token == "1" || token.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("yes", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("on", System.StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (token == "0" || token.Equals("false", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("no", System.StringComparison.OrdinalIgnoreCase) ||
                token.Equals("off", System.StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }

        private static string GetLeaf(string mayaName)
        {
            if (string.IsNullOrEmpty(mayaName)) return null;
            var s = mayaName;
            var idx = s.LastIndexOf('|');
            if (idx >= 0 && idx + 1 < s.Length) s = s.Substring(idx + 1);
            return s;
        }
    }
}
