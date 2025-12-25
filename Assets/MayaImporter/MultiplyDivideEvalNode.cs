using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    public class MultiplyDivideEvalNode : EvalNode
    {
        private readonly MayaNode _node;

        public MultiplyDivideEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _node = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            int op = GetInt("operation", 1);

            Vector3 in1 = GetVec("input1", Vector3.one);
            Vector3 in2 = GetVec("input2", Vector3.one);

            Vector3 outv = op switch
            {
                2 => new Vector3(
                        SafeDiv(in1.x, in2.x),
                        SafeDiv(in1.y, in2.y),
                        SafeDiv(in1.z, in2.z)),
                3 => new Vector3(
                        Mathf.Pow(in1.x, in2.x),
                        Mathf.Pow(in1.y, in2.y),
                        Mathf.Pow(in1.z, in2.z)),
                _ => Vector3.Scale(in1, in2)
            };

            SetVec("output", outv);

            ctx?.MarkAttributeDirty($"{NodeName}.output");
        }

        // -------- helpers --------

        private int GetInt(string k, int def)
        {
            if (_node.Attributes.TryGetValue(k, out var a) && a.Data?.Value is int i)
                return i;
            return def;
        }

        private Vector3 GetVec(string prefix, Vector3 def)
        {
            float x = GetFloat(prefix + "X", def.x);
            float y = GetFloat(prefix + "Y", def.y);
            float z = GetFloat(prefix + "Z", def.z);
            return new Vector3(x, y, z);
        }

        private void SetVec(string prefix, Vector3 v)
        {
            _node.Attributes[prefix + "X"].Data.Value = v.x;
            _node.Attributes[prefix + "Y"].Data.Value = v.y;
            _node.Attributes[prefix + "Z"].Data.Value = v.z;
        }

        private float GetFloat(string k, float def)
        {
            if (_node.Attributes.TryGetValue(k, out var a))
            {
                if (a.Data?.Value is float f) return f;
                if (a.Data?.Value is int i) return i;
            }
            return def;
        }

        private float SafeDiv(float a, float b)
        {
            return Mathf.Abs(b) < 1e-6f ? 0f : a / b;
        }
    }
}
