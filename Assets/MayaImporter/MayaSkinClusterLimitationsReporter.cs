using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// SkinCluster ̊čB
    /// 100%j:
    /// - UnityKpߎ(4{Ȃ)łAFullWeightsێɂf[^̓[
    /// -  Blocker ͏o Warn/Info ̂
    /// </summary>
    public static class MayaSkinClusterLimitationsReporter
    {
        [Serializable]
        public sealed class SkinLimitationRow
        {
            public string SkinClusterName;

            // Compatibility: older reporter expected SkinClusterNodeName
            public string SkinClusterNodeName { get => SkinClusterName; set => SkinClusterName = value; }
            public string IssueKey;
            public string Severity; // Info / Warn
            public string Details;
        }

        public static List<SkinLimitationRow> Collect(MayaSceneData scene)
        {
            var outList = new List<SkinLimitationRow>(64);
            if (scene == null || scene.Nodes == null) return outList;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;
                if (!string.Equals(n.NodeType, "skinCluster", StringComparison.Ordinal)) continue;

                CollectFor(n, outList);
            }

            return outList;
        }

        private static void CollectFor(NodeRecord skin, List<SkinLimitationRow> outList)
        {
            // 1) Unity4{
            outList.Add(new SkinLimitationRow
            {
                SkinClusterName = skin.Name,
                IssueKey = "Unity_BoneWeight_Limit4",
                Severity = "Warn",
                Details = "Unity BoneWeight is typically limited to 4 influences per vertex. Tool preserves full weights in MayaSkinClusterComponent.FullWeights and applies top-4 weights to SkinnedMeshRenderer as approximation."
            });

            // 2) DualQuaternion
            outList.Add(new SkinLimitationRow
            {
                SkinClusterName = skin.Name,
                IssueKey = "Maya_Skinning_Method",
                Severity = "Info",
                Details = "Maya skinning method (Classic Linear / Dual Quaternion) may not map 1:1 to Unity depending on runtime solver. Raw attributes are preserved; implement a DQ solver if strict equivalence is required."
            });
        }
    }
}