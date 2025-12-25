using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    public class ClampEvalNode : EvalNode
    {
        private readonly MayaNode _node;

        public ClampEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _node = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            Vector3 input = GetVec("input", Vector3.zero);
            Vector3 minv = GetVec("min", Vector3.zero);
            Vector3 maxv = GetVec("max", Vector3.one);

            Vector3 outv = new Vector3(
                Mathf.Clamp(input.x, minv.x, maxv.x),
                Mathf.Clamp(input.y, minv.y, maxv.y),
                Mathf.Clamp(input.z, minv.z, maxv.z));

            SetVec("output", outv);

            ctx?.MarkAttributeDirty($"{NodeName}.output");
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
