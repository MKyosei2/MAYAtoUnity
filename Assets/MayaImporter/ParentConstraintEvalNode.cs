using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Phase3.Evaluation
{
    public class ParentConstraintEvalNode : EvalNode
    {
        private readonly Transform _constrained;
        private readonly List<Transform> _targets;
        private readonly List<Matrix4x4> _offsets;

        // animated weight
        private readonly List<WeightEvalNode> _weightNodes;

        // fallback weight（animCurve が無い場合）
        private readonly List<float> _defaultWeights;

        public ParentConstraintEvalNode(
            string nodeName,
            Transform constrained,
            List<Transform> targets,
            List<Matrix4x4> offsets,
            List<WeightEvalNode> weightNodes,
            List<float> defaultWeights)
            : base(nodeName)
        {
            _constrained = constrained;
            _targets = targets;
            _offsets = offsets;
            _weightNodes = weightNodes;
            _defaultWeights = defaultWeights;

            // weight → constraint の依存（Dirty を自然に伝播）
            for (int i = 0; i < _weightNodes.Count; i++)
            {
                if (_weightNodes[i] != null)
                    AddInput(_weightNodes[i]);
            }
        }

        protected override void Evaluate(EvalContext ctx)
        {
            if (_constrained == null || _targets == null || _targets.Count == 0)
                return;

            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            float total = 0f;

            int count = _targets.Count;
            for (int i = 0; i < count; i++)
            {
                var t = _targets[i];
                if (t == null) continue;

                float w = (_weightNodes[i] != null)
                    ? _weightNodes[i].Value
                    : _defaultWeights[i];

                if (w <= 0f) continue;

                total += w;

                var m = t.localToWorldMatrix * _offsets[i];
                Vector3 p = new Vector3(m.m03, m.m13, m.m23);

                pos += p * w;
                rot = Quaternion.Slerp(
                    rot,
                    m.rotation,
                    w / Mathf.Max(total, Mathf.Epsilon));
            }

            if (total <= 0f)
                return;

            _constrained.position = pos / total;
            _constrained.rotation = rot;
        }
    }
}
