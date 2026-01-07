// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
// Assets/MayaImporter/MayaConstraintConnectionResolver.cs
using System;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Constraint connections differ across Maya versions/exporters:
    /// - targetParentMatrix / targetMatrix / targetWorldMatrix
    /// - targetTranslate / targetRotate / targetScale
    /// - sometimes no connections, only string attrs
    ///
    /// This resolver centralizes:
    /// - target node resolving
    /// - constrained node resolving
    /// - worldUp object resolving
    ///
    /// IMPORTANT:
    /// MayaNodeComponentBase.TryGetAttr is protected.
    /// We must access node.Attributes directly.
    /// </summary>
    public static class MayaConstraintConnectionResolver
    {
        // Target side suffix candidates (most common first)
        private static readonly string[] TargetSuffixCandidates =
        {
            // Matrices
            "targetParentMatrix",
            "targetMatrix",
            "targetWorldMatrix",
            "targetWorldMatrix[0]",
            "targetParentMatrix[0]",
            "targetMatrix[0]",

            // TRS channels
            "targetTranslate",
            "targetRotate",
            "targetScale",

            // Seen in some exporters / variations
            "targetTransform",
            "target"
        };

        // WorldUp object side candidates (aimConstraint etc.)
        private static readonly string[] WorldUpSuffixCandidates =
        {
            "wuo",
            "worldUpMatrix",
            "wum",
            "worldUpParentMatrix",
            "worldUpObject"
        };

        public enum ConstrainedKind
        {
            Parent,
            Point,
            Orient,
            Aim,
            Scale
        }

        public static string ResolveTargetNodeName(MayaNodeComponentBase constraintNode, int targetIndex)
        {
            if (constraintNode == null) return null;

            // 1) Try connections: .tg[i].<suffix>
            for (int si = 0; si < TargetSuffixCandidates.Length; si++)
            {
                string suf = $".tg[{targetIndex}].{TargetSuffixCandidates[si]}";
                var src = ResolveIncomingSourceNode(constraintNode, suf);
                if (!string.IsNullOrEmpty(src)) return src;
            }

            // 2) Try explicit string attrs if present (best-effort)
            // Some .ma store target name-like strings on tg[i].tn or similar
            if (TryReadStringAttr(constraintNode, $".tg[{targetIndex}].tn", out var s) && !string.IsNullOrEmpty(s)) return s;
            if (TryReadStringAttr(constraintNode, $".tg[{targetIndex}].target", out s) && !string.IsNullOrEmpty(s)) return s;

            // 3) Deterministic fallback
            return $"target_{targetIndex}";
        }

        public static string ResolveWorldUpObjectNodeName(MayaNodeComponentBase constraintNode)
        {
            if (constraintNode == null) return null;

            for (int i = 0; i < WorldUpSuffixCandidates.Length; i++)
            {
                var src = ResolveIncomingSourceNode(constraintNode, "." + WorldUpSuffixCandidates[i]);
                if (!string.IsNullOrEmpty(src)) return src;
            }

            return null;
        }

        /// <summary>
        /// Resolve constrained node for point/orient/parent/scale/aim constraints.
        /// Looks at outgoing connections from constraint node into a DAG node.
        /// </summary>
        public static string ResolveConstrainedNodeName(MayaNodeComponentBase constraintNode, ConstrainedKind kind)
        {
            if (constraintNode == null) return null;
            if (constraintNode.Connections == null || constraintNode.Connections.Count == 0) return null;

            string best = null;

            for (int i = 0; i < constraintNode.Connections.Count; i++)
            {
                var c = constraintNode.Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Source &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";

                bool hit = kind switch
                {
                    ConstrainedKind.Point => LooksLikeTranslate(dstAttr),
                    ConstrainedKind.Orient => LooksLikeRotate(dstAttr),
                    ConstrainedKind.Aim => LooksLikeRotate(dstAttr),
                    ConstrainedKind.Scale => LooksLikeScale(dstAttr),
                    ConstrainedKind.Parent => LooksLikeTranslate(dstAttr) || LooksLikeRotate(dstAttr) || LooksLikeScale(dstAttr),
                    _ => false
                };

                if (hit)
                    return c.DstNodePart;

                best ??= c.DstNodePart;
            }

            return best;
        }

        /// <summary>
        /// Find the incoming connection (src node) that drives a given destination plug suffix.
        /// </summary>
        public static string ResolveIncomingSourceNode(MayaNodeComponentBase node, string dstPlugSuffix)
        {
            if (node == null) return null;
            if (node.Connections == null || node.Connections.Count == 0) return null;

            var sufDot = NormalizeSuffix(dstPlugSuffix);

            for (int i = 0; i < node.Connections.Count; i++)
            {
                var c = node.Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                if (EndsWithSuffixCompat(c.DstPlug, sufDot))
                {
                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }

        // --------------------------
        // Attribute reading (no TryGetAttr; it's protected)
        // --------------------------

        private static bool TryFindAttr(MayaNodeComponentBase node, string key, out MayaNodeComponentBase.SerializedAttribute attr)
        {
            attr = null;
            if (node == null || node.Attributes == null || node.Attributes.Count == 0) return false;
            if (string.IsNullOrEmpty(key)) return false;

            // exact
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, key, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            // dot-compat
            var dot = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : "." + key;
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var a = node.Attributes[i];
                if (a == null) continue;
                if (string.Equals(a.Key, dot, StringComparison.Ordinal))
                {
                    attr = a;
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadStringAttr(MayaNodeComponentBase node, string key, out string value)
        {
            value = null;
            if (!TryFindAttr(node, key, out var a) || a == null || a.Tokens == null || a.Tokens.Count == 0)
                return false;

            var s = a.Tokens[0];
            if (string.IsNullOrEmpty(s)) return false;

            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                s = s.Substring(1, s.Length - 2);

            value = s;
            return true;
        }

        // --------------------------
        // Plug heuristics
        // --------------------------

        private static bool LooksLikeTranslate(string dstAttr)
        {
            if (string.IsNullOrEmpty(dstAttr)) return false;
            return dstAttr.StartsWith("translate", StringComparison.Ordinal) ||
                   dstAttr == "t" || dstAttr == "tx" || dstAttr == "ty" || dstAttr == "tz";
        }

        private static bool LooksLikeRotate(string dstAttr)
        {
            if (string.IsNullOrEmpty(dstAttr)) return false;
            return dstAttr.StartsWith("rotate", StringComparison.Ordinal) ||
                   dstAttr == "r" || dstAttr == "rx" || dstAttr == "ry" || dstAttr == "rz";
        }

        private static bool LooksLikeScale(string dstAttr)
        {
            if (string.IsNullOrEmpty(dstAttr)) return false;
            return dstAttr.StartsWith("scale", StringComparison.Ordinal) ||
                   dstAttr == "s" || dstAttr == "sx" || dstAttr == "sy" || dstAttr == "sz";
        }

        private static string NormalizeSuffix(string s)
            => string.IsNullOrEmpty(s) ? s : (s.StartsWith(".", StringComparison.Ordinal) ? s : "." + s);

        private static bool EndsWithSuffixCompat(string plug, string suffixWithDot)
        {
            if (plug.EndsWith(suffixWithDot, StringComparison.Ordinal)) return true;
            var noDot = suffixWithDot.StartsWith(".", StringComparison.Ordinal) ? suffixWithDot.Substring(1) : suffixWithDot;
            return plug.EndsWith(noDot, StringComparison.Ordinal);
        }
    }
}
