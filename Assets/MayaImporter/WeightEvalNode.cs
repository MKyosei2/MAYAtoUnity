using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// animCurve を ctx.Time でサンプルして float 値を返す（本実装）
    /// </summary>
    public sealed class WeightEvalNode : EvalNode
    {
        private readonly float[] _times;
        private readonly float[] _values;

        private float _value;
        public float Value => _value;

        public WeightEvalNode(MayaNode animCurveNode)
            : base(animCurveNode.NodeName)
        {
            _times = ExtractFloatArray(animCurveNode, "keyTime");
            _values = ExtractFloatArray(animCurveNode, "keyValue");
        }

        protected override void Evaluate(EvalContext ctx)
        {
            if (ctx == null || _times == null || _values == null || _times.Length == 0 || _values.Length == 0)
            {
                _value = 0f;
                return;
            }

            float t = ctx.Time;

            // 端
            if (t <= _times[0]) { _value = _values[0]; return; }
            int last = Mathf.Min(_times.Length, _values.Length) - 1;
            if (t >= _times[last]) { _value = _values[last]; return; }

            // 区間探索（線形補間：まずは本実装の最低ライン）
            // ※ 将来的にタンジェント補間へ拡張可能
            int i = 0;
            while (i < last && _times[i + 1] < t) i++;

            float t0 = _times[i];
            float t1 = _times[i + 1];
            float v0 = _values[i];
            float v1 = _values[i + 1];

            float u = (t - t0) / Mathf.Max(t1 - t0, 1e-8f);
            _value = Mathf.Lerp(v0, v1, u);
        }

        private static float[] ExtractFloatArray(MayaNode node, string key)
        {
            if (node.Attributes.TryGetValue(key, out var a) && a.Data?.Value is float[] f)
                return f;
            return null;
        }
    }
}
