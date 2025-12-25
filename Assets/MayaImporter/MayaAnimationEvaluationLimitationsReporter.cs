using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// Reports limitations for Maya evaluation/animation equivalence in Unity.
    /// Portfolio policy:
    /// - Do not mark as Blocker easily
    /// - Preserve data (100%) and warn about runtime equivalence gaps
    /// </summary>
    public static class MayaAnimationEvaluationLimitationsReporter
    {
        [Serializable]
        public sealed class AnimLimitationRow
        {
            public string Scope;      // e.g. "AnimationEvaluation"
            public string IssueKey;
            public string Severity;   // Info / Warn
            public string Details;
        }

        public static List<AnimLimitationRow> Collect(MayaSceneData scene)
        {
            var list = new List<AnimLimitationRow>(16);
            if (scene == null || scene.Nodes == null) return list;

            bool hasConstraints = false;
            bool hasExpressions = false;
            bool hasAnimCurves = false;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;

                var t = n.NodeType ?? "";
                if (t.StartsWith("animCurve", StringComparison.Ordinal))
                    hasAnimCurves = true;
                if (t.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0)
                    hasConstraints = true;
                if (string.Equals(t, "expression", StringComparison.OrdinalIgnoreCase))
                    hasExpressions = true;
            }

            if (hasAnimCurves)
            {
                list.Add(new AnimLimitationRow
                {
                    Scope = "AnimationEvaluation",
                    IssueKey = "AnimCurves_Present",
                    Severity = "Info",
                    Details = "animCurve nodes exist. Unity can play clips, but exact Maya curve evaluation (tangents/units) may require a dedicated curve decoder. Raw data is preserved."
                });
            }

            if (hasConstraints)
            {
                list.Add(new AnimLimitationRow
                {
                    Scope = "AnimationEvaluation",
                    IssueKey = "Constraints_Present",
                    Severity = "Warn",
                    Details = "constraint nodes exist. Maya results depend on solver order/evaluation graph. Unity requires runtime solver components to match Maya. Data preserved."
                });
            }

            if (hasExpressions)
            {
                list.Add(new AnimLimitationRow
                {
                    Scope = "AnimationEvaluation",
                    IssueKey = "Expressions_Present",
                    Severity = "Warn",
                    Details = "expression nodes exist. Unity requires expression evaluation runtime to match Maya. Data preserved."
                });
            }

            if (hasConstraints || hasExpressions)
            {
                list.Add(new AnimLimitationRow
                {
                    Scope = "AnimationEvaluation",
                    IssueKey = "EvalGraph_NotImplemented",
                    Severity = "Warn",
                    Details = "Strict Maya evaluation order/graph is not implemented yet. Pipeline guarantees full data preservation + GameObject reconstruction; exact runtime equivalence needs eval graph + solvers."
                });
            }

            return list;
        }
    }
}
