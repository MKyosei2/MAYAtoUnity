// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// �]���P�ʁinode�j
    /// attribute �P�ʂ̈ˑ������āA�K�v�ȂƂ������]������
    /// </summary>
    public abstract class EvalNode
    {
        public string NodeName { get; }

        // -----------------------------
        // Dirty �Ǘ��inode �P�ʁj
        // -----------------------------
        public bool Dirty { get; private set; } = true;

        // -----------------------------
        // �ˑ��֌W�inode�j
        // -----------------------------
        protected readonly List<EvalNode> _inputs = new();
        public IReadOnlyList<EvalNode> Inputs => _inputs;

        // -----------------------------
        // �� attribute �P�ʂ̓��͈ˑ�
        // -----------------------------
        private readonly HashSet<string> _inputAttributes = new();
        public IReadOnlyCollection<string> InputAttributes => _inputAttributes;

        protected EvalNode(string nodeName)
        {
            NodeName = nodeName;
        }

        // -----------------------------
        // Dependency �o�^
        // -----------------------------
        public void AddInput(EvalNode node)
        {
            if (!_inputs.Contains(node))
                _inputs.Add(node);
        }

        public void AddInputAttribute(string attrPath)
        {
            if (!string.IsNullOrEmpty(attrPath))
                _inputAttributes.Add(attrPath);
        }

        // -----------------------------
        // Dirty ����
        // -----------------------------
        public void MarkDirty()
        {
            if (Dirty)
                return;

            Dirty = true;

            // �����֓`�d�inode �P�ʁj
            foreach (var n in _inputs)
                n.MarkDirty();
        }

        private void ClearDirty()
        {
            Dirty = false;
        }

        // -----------------------------
        // Evaluation�i���{�����j
        // -----------------------------
        public void EvaluateIfNeeded(EvalContext ctx)
        {
            // 1. node �� Dirty �łȂ� �� �������Ȃ�
            if (!Dirty)
                return;

            // 2. attribute �ˑ�������ꍇ�A
            //    �������ˑ����Ă��� attribute ���ύX����Ă��Ȃ���� skip
            if (_inputAttributes.Count > 0 &&
                ctx != null &&
                ctx.HasAnyDirtyAttributes())
            {
                bool hit = false;

                foreach (var attr in _inputAttributes)
                {
                    if (ctx.IsAttributeDirty(attr))
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    // attribute �I�ɖ��֌W �� Dirty �������� skip
                    ClearDirty();
                    return;
                }
            }

            // 3. �]�����s
            Evaluate(ctx);

            // 4. ������ output attribute �� dirty ����
            if (ctx != null)
                ctx.MarkAttributeDirty(NodeName);

            ClearDirty();
        }

        // -----------------------------
        // ������
        // -----------------------------
        protected abstract void Evaluate(EvalContext ctx);
    }
}


// ----------------------------------------------------------------------------- 
// INTEGRATED: WeightEvalNode.cs (moved here; shared eval helper)
// -----------------------------------------------------------------------------
// PATCH: ProductionImpl v6 (Unity-only, retention-first)

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// animCurve �� ctx.Time �ŃT���v������ float �l��Ԃ��i�{�����j
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

            // �[
            if (t <= _times[0]) { _value = _values[0]; return; }
            int last = Mathf.Min(_times.Length, _values.Length) - 1;
            if (t >= _times[last]) { _value = _values[last]; return; }

            // ��ԒT���i���`��ԁF�܂��͖{�����̍Œ჉�C���j
            // �� �����I�Ƀ^���W�F���g��Ԃ֊g���\
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
