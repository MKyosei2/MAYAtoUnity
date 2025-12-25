using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Phase3.Evaluation
{
    public class AimConstraintEvalNode : EvalNode
    {
        private readonly Transform _constrained;
        private readonly List<Transform> _targets;
        private readonly List<Quaternion> _offsets;
        private readonly List<WeightEvalNode> _weightNodes;
        private readonly List<float> _defaultWeights;

        private readonly Vector3 _aimVector;
        private readonly Vector3 _upVector;
        private readonly Transform _worldUpObject;

        public AimConstraintEvalNode(
            string nodeName,
            Transform constrained,
            List<Transform> targets,
            List<Quaternion> offsets,
            List<WeightEvalNode> weightNodes,
            List<float> defaultWeights,
            Vector3 aimVector,
            Vector3 upVector,
            Transform worldUpObject)
            : base(nodeName)
        {
            _constrained = constrained;
            _targets = targets;
            _offsets = offsets;
            _weightNodes = weightNodes;
            _defaultWeights = defaultWeights;
            _aimVector = aimVector.normalized;
            _upVector = upVector.normalized;
            _worldUpObject = worldUpObject;

            for (int i = 0; i < _weightNodes.Count; i++)
                if (_weightNodes[i] != null)
                    AddInput(_weightNodes[i]);
        }

        protected override void Evaluate(EvalContext ctx)
        {
            Vector3 aimDir = Vector3.zero;
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
                aimDir += (t.position - _constrained.position).normalized * w;
            }

            if (total <= 0f)
                return;

            aimDir.Normalize();

            Vector3 up = _worldUpObject != null
                ? _worldUpObject.up
                : Vector3.up;

            Quaternion aimRot = Quaternion.LookRotation(aimDir, up);

            // Maya ‚Ì aimVector / upVector ‚ðl—¶
            Quaternion axisAdjust = Quaternion.FromToRotation(Vector3.forward, _aimVector);
            Quaternion finalRot = aimRot * Quaternion.Inverse(axisAdjust);

            // offsetimaintainOffsetj
            finalRot *= _offsets[0];

            _constrained.rotation = finalRot;
        }
    }
}
