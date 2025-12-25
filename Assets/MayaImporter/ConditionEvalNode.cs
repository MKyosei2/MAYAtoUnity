using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    public class ConditionEvalNode : EvalNode
    {
        private readonly MayaNode _node;

        public ConditionEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _node = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            float first = GetFloat("firstTerm", 0f);
            float second = GetFloat("secondTerm", 0f);
            int op = GetInt("operation", 0);

            bool result = op switch
            {
                1 => first != second,
                2 => first > second,
                3 => first >= second,
                4 => first < second,
                5 => first <= second,
                _ => Mathf.Approximately(first, second)
            };

            Vector3 outColor = result
                ? GetVec("colorIfTrue", Vector3.zero)
                : GetVec("colorIfFalse", Vector3.zero);

            SetVec("outColor", outColor);

            ctx?.MarkAttributeDirty($"{NodeName}.outColor");
        }

        // ---------------- helpers ----------------

        private float GetFloat(string k, float def)
        {
            if (_node.Attributes.TryGetValue(k, out var a))
            {
                if (a.Data?.Value is float f) return f;
                if (a.Data?.Value is int i) return i;
            }
            return def;
        }

        private int GetInt(string k, int def)
        {
            if (_node.Attributes.TryGetValue(k, out var a) && a.Data?.Value is int i)
                return i;
            return def;
        }

        private Vector3 GetVec(string prefix, Vector3 def)
        {
            float x = GetFloat(prefix + "R", def.x);
            float y = GetFloat(prefix + "G", def.y);
            float z = GetFloat(prefix + "B", def.z);
            return new Vector3(x, y, z);
        }

        private void SetVec(string prefix, Vector3 v)
        {
            _node.Attributes[prefix + "R"].Data.Value = v.x;
            _node.Attributes[prefix + "G"].Data.Value = v.y;
            _node.Attributes[prefix + "B"].Data.Value = v.z;
        }
    }
}
