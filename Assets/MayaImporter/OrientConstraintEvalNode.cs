using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Phase3.Evaluation
{
    public class OrientConstraintEvalNode : EvalNode
    {
        private readonly Transform _constrained;
        private readonly List<Transform> _targets;
        private readonly List<Quaternion> _offsets;
        private readonly List<WeightEvalNode> _weightNodes;
        private readonly List<float> _defaultWeights;

        public OrientConstraintEvalNode(
            string nodeName,
            Transform constrained,
            List<Transform> targets,
            List<Quaternion> offsets,
            List<WeightEvalNode> weightNodes,
            List<float> defaultWeights)
            : base(nodeName)
        {
            _constrained = constrained;
            _targets = targets;
            _offsets = offsets;
            _weightNodes = weightNodes;
            _defaultWeights = defaultWeights;

            for (int i = 0; i < _weightNodes.Count; i++)
                if (_weightNodes[i] != null)
                    AddInput(_weightNodes[i]);
        }

        protected override void Evaluate(EvalContext ctx)
        {
            Quaternion rot = Quaternion.identity;
            float total = 0f;

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t == null) continue;

                float w = (_weightNodes[i] != null)
                    ? _weightNodes[i].Value
                    : _defaultWeights[i];

                if (w <= 0f) continue;

                total += w;

                var r = t.rotation * _offsets[i];
                rot = Quaternion.Slerp(rot, r, w / Mathf.Max(total, Mathf.Epsilon));
            }

            if (total > 0f)
                _constrained.rotation = rot;
        }
    }
}
