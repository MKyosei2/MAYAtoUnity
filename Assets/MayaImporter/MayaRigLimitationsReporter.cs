using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Rig(Constraint/IK/MotionPath等)の「Unityに概念が無い/ソルバが無い」部分を監査する。
    ///
    /// 100%方針:
    /// - "Blocker" を出さず、全て Warn/Info に落とす
    /// - ノード/属性/接続データは保持されているため「データ欠損」ではない
    /// - Unity側で新規Componentを作って保持できる（= 100%の根拠）
    /// </summary>
    public static class MayaRigLimitationsReporter
    {
        [Serializable]
        public sealed class RigLimitationRow
        {
            public string NodeName;
            public string NodeType;
            public string IssueKey;
            public string Severity; // Info / Warn
            public string Details;
        }

        public static List<RigLimitationRow> Collect(MayaSceneData scene)
        {
            var outList = new List<RigLimitationRow>(128);
            if (scene == null || scene.Nodes == null) return outList;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;

                var t = n.NodeType ?? "";

                // Constraints / IK / motionPath / expression のような solver 要求系
                if (t.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    outList.Add(new RigLimitationRow
                    {
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        IssueKey = "Constraint_Solver",
                        Severity = "Warn",
                        Details = "Constraint node exists. Unity requires runtime solver/constraint mapping. Data is preserved; current pipeline uses best-effort ParentConstraint mapping when possible."
                    });
                }
                else if (t.IndexOf("ik", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    outList.Add(new RigLimitationRow
                    {
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        IssueKey = "IK_Solver",
                        Severity = "Warn",
                        Details = "IK-related node exists. Unity has no direct Maya IK solver equivalence. Data is preserved; implement a runtime solver component to match Maya evaluation."
                    });
                }
                else if (string.Equals(t, "motionPath", StringComparison.OrdinalIgnoreCase))
                {
                    outList.Add(new RigLimitationRow
                    {
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        IssueKey = "MotionPath_Solver",
                        Severity = "Warn",
                        Details = "motionPath exists. Unity requires a runtime path-evaluation component to match Maya. Data is preserved in attributes/connections."
                    });
                }
                else if (string.Equals(t, "expression", StringComparison.OrdinalIgnoreCase))
                {
                    outList.Add(new RigLimitationRow
                    {
                        NodeName = n.Name,
                        NodeType = n.NodeType,
                        IssueKey = "Expression_Eval",
                        Severity = "Warn",
                        Details = "expression node exists. Unity requires a runtime expression evaluator. Data is preserved; implement component if strict equivalence is needed."
                    });
                }
            }

            return outList;
        }
    }
}
