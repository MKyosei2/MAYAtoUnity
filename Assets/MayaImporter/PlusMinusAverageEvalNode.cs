using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Phase3.Evaluation
{
    public class PlusMinusAverageEvalNode : EvalNode
    {
        private readonly MayaNode _node;

        public PlusMinusAverageEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _node = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            int op = GetInt("operation", 1);

            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var kv in _node.Attributes)
            {
                if (!kv.Key.StartsWith("input3D["))
                    continue;

                if (kv.Value.Data?.Value is float[] f && f.Length >= 3)
                {
                    sum += new Vector3(f[0], f[1], f[2]);
                    count++;
                }
            }

            Vector3 outv = op switch
            {
                2 => count > 0 ? -sum : Vector3.zero,
                3 => count > 0 ? sum / count : Vector3.zero,
                _ => sum
            };

            SetVec("output3D", outv);

            ctx?.MarkAttributeDirty($"{NodeName}.output3D");
        }

        // -------- helpers --------

        private int GetInt(string k, int def)
        {
            if (_node.Attributes.TryGetValue(k, out var a) && a.Data?.Value is int i)
                return i;
            return def;
        }

        private void SetVec(string prefix, Vector3 v)
        {
            _node.Attributes[prefix + "[0]"].Data.Value = new float[] { v.x, v.y, v.z };
        }
    }
}
