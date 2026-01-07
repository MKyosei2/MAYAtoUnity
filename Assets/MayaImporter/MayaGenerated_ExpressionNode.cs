// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// Assets/MayaImporter/MayaGenerated_ExpressionNode.cs
// Phase5 override:
// - Preserve full expression source string (Maya: expression node).
// - Parse a practical subset of assignments and configure MayaExpressionRuntime (Unity-only).
//
// NodeType: expression

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using MayaImporter.Animation;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("expression")]
    public sealed class MayaGenerated_ExpressionNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (expression)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private string expressionTextPreview;

        [SerializeField] private int parsedAssignmentCount = 0;

        // Generic connection hints (best-effort)
        [SerializeField] private string lastIncomingToInput;
        [SerializeField] private string lastIncomingToTime;

        private static readonly Regex AssignRegex = new Regex(
            // lhs: node.attr, rhs: expression text until ';'
            @"(?<lhs>[^=;\r\n]+?)\s*=\s*(?<rhs>[^;\r\n]+)\s*;",
            RegexOptions.Compiled);

        private static readonly Regex LhsTransformAttrRegex = new Regex(
            @"^(?<node>[\w\|\:\.]+)\.(?<attr>tx|ty|tz|rx|ry|rz|sx|sy|sz)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // enable heuristics
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            // Fetch expression text (attribute name differs across Maya versions/exports; best-effort scan)
            var expr = ReadString("",
                ".s", "s",
                ".expression", "expression",
                ".expr", "expr",
                ".e", "e");

            if (expr == null) expr = "";

            expressionTextPreview = BuildPreview(expr, 300);

            // Connection hints
            lastIncomingToInput = FindLastIncomingTo("input", "in", "i");
            lastIncomingToTime = FindLastIncomingTo("time", "t");

            // Parse subset assignments
            var assigns = ParseTransformAssignments(expr);
            parsedAssignmentCount = assigns.Count;

            // Configure runtime evaluator (Unity-only)
            var rt = GetComponent<MayaExpressionRuntime>();
            if (rt == null) rt = gameObject.AddComponent<MayaExpressionRuntime>();

            rt.execute = enabled;
            rt.conversion = options != null ? options.Conversion : CoordinateConversion.MayaToUnity_MirrorZ;
            rt.Configure(NodeName, expr, assigns.ToArray(), rt.conversion);

            // Notes for audit window
            string inInput = string.IsNullOrEmpty(lastIncomingToInput) ? "none" : lastIncomingToInput;
            string inTime = string.IsNullOrEmpty(lastIncomingToTime) ? "none" : lastIncomingToTime;

            SetNotes(
                $"expression '{NodeName}' decoded: enabled={enabled}, parsedAssignments={parsedAssignmentCount}, " +
                $"incomingInput={inInput}, incomingTime={inTime}. " +
                $"(Phase5: subset evaluator runs in Unity-only environment)");
        }

        private static string BuildPreview(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            s = s.Replace("\r\n", "\n");
            if (s.Length <= maxChars) return s;
            return s.Substring(0, Mathf.Max(0, maxChars)) + "\n...(truncated)...";
        }

        private static List<MayaExpressionRuntime.Assignment> ParseTransformAssignments(string expr)
        {
            var list = new List<MayaExpressionRuntime.Assignment>(8);
            if (string.IsNullOrEmpty(expr)) return list;

            // Ensure we have semicolons (Maya expressions usually do)
            // If not, append one to help regex match (best-effort).
            if (!expr.Contains(";")) expr = expr + ";";

            var m = AssignRegex.Matches(expr);
            for (int i = 0; i < m.Count; i++)
            {
                var lhs = (m[i].Groups["lhs"].Value ?? "").Trim();
                var rhs = (m[i].Groups["rhs"].Value ?? "").Trim();
                if (string.IsNullOrEmpty(lhs) || string.IsNullOrEmpty(rhs)) continue;

                // remove optional leading "float " / "int " style declarations
                if (lhs.StartsWith("float ", StringComparison.OrdinalIgnoreCase)) lhs = lhs.Substring(6).Trim();
                if (lhs.StartsWith("int ", StringComparison.OrdinalIgnoreCase)) lhs = lhs.Substring(4).Trim();

                // Only transform channel assignments are executed.
                var lm = LhsTransformAttrRegex.Match(lhs);
                if (!lm.Success) continue;

                var node = lm.Groups["node"].Value;
                var attr = lm.Groups["attr"].Value.ToLowerInvariant();

                // Reject self-node assignment like ".tx" without node (unsupported)
                if (string.IsNullOrEmpty(node)) continue;

                list.Add(new MayaExpressionRuntime.Assignment
                {
                    targetNodeName = node,
                    targetAttr = attr,
                    rhsExpression = rhs
                });
            }

            return list;
        }
    }
}
