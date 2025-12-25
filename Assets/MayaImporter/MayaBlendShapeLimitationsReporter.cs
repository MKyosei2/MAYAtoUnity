using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    /// <summary>
    /// BlendShape の監査。
    /// 100%方針:
    /// - 複雑ケース(in-between, component based deltas 等)は Unity標準だけでは再現困難
    /// - しかし raw tokens と MayaBlendShapeComponent に保持されている限り、データ欠損ではない
    /// - よって Blocker は出さず Warn/Info として「要実装箇所」を提示する
    /// </summary>
    public static class MayaBlendShapeLimitationsReporter
    {
        [Serializable]
        public sealed class BlendLimitationRow
        {
            public string BlendShapeNodeName;
            public string IssueKey;
            public string Severity; // Info / Warn
            public string Details;
        }

        public static List<BlendLimitationRow> Collect(MayaSceneData scene)
        {
            var outList = new List<BlendLimitationRow>(64);
            if (scene == null || scene.Nodes == null) return outList;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;
                if (!string.Equals(n.NodeType, "blendShape", StringComparison.Ordinal)) continue;

                outList.Add(new BlendLimitationRow
                {
                    BlendShapeNodeName = n.Name,
                    IssueKey = "BlendShape_Cases",
                    Severity = "Warn",
                    Details =
                        "blendShape nodes can contain inputGeomTarget/component-based deltas/in-between shapes. Unity supports standard blendshapes but may require additional decoding/runtime for strict Maya equivalence. Raw attributes are preserved."
                });
            }

            return outList;
        }
    }
}
