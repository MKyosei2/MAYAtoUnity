// MayaImporter/ConditionNode.cs
// NodeType: condition
// Production (real decode + value publish)
//
// Maya condition (best-effort):
// - firstTerm, secondTerm, operation
// - colorIfTrue / colorIfFalse (RGB)
// - alphaIfTrue / alphaIfFalse
// outputs: outColor (RGB) + outAlpha
//
// Publishes:
// - MayaVector3Value : outColor (Vector)
// - MayaFloatValue   : outAlpha

using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    [DisallowMultipleComponent]
    [MayaNodeType("condition")]
    public sealed class ConditionNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (condition)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private int operation = 0;
        [SerializeField] private float firstTerm = 0f;
        [SerializeField] private float secondTerm = 0f;

        [SerializeField] private Vector3 colorIfTrue = Vector3.one;
        [SerializeField] private Vector3 colorIfFalse = Vector3.zero;
        [SerializeField] private float alphaIfTrue = 1f;
        [SerializeField] private float alphaIfFalse = 0f;

        [Header("Incoming (best-effort)")]
        [SerializeField] private string incomingFirstTerm;
        [SerializeField] private string incomingSecondTerm;
        [SerializeField] private string incomingColorIfTrue;
        [SerializeField] private string incomingColorIfFalse;

        [Header("Output (computed)")]
        [SerializeField] private bool result = false;
        [SerializeField] private Vector3 outColor = Vector3.zero;
        [SerializeField] private float outAlpha = 0f;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            operation = ReadInt(0, ".operation", "operation", ".op", "op");
            firstTerm = ReadFloat(0f, ".firstTerm", "firstTerm", ".ft", "ft");
            secondTerm = ReadFloat(0f, ".secondTerm", "secondTerm", ".st", "st");

            colorIfTrue = ReadVec3(
                def: Vector3.one,
                packedKeys: new[] { ".colorIfTrue", "colorIfTrue", ".ct", "ct" },
                xKeys: new[] { ".colorIfTrueR", "colorIfTrueR", ".ctr", "ctr", ".colorIfTrueX", "colorIfTrueX" },
                yKeys: new[] { ".colorIfTrueG", "colorIfTrueG", ".ctg", "ctg", ".colorIfTrueY", "colorIfTrueY" },
                zKeys: new[] { ".colorIfTrueB", "colorIfTrueB", ".ctb", "ctb", ".colorIfTrueZ", "colorIfTrueZ" }
            );

            colorIfFalse = ReadVec3(
                def: Vector3.zero,
                packedKeys: new[] { ".colorIfFalse", "colorIfFalse", ".cf", "cf" },
                xKeys: new[] { ".colorIfFalseR", "colorIfFalseR", ".cfr", "cfr", ".colorIfFalseX", "colorIfFalseX" },
                yKeys: new[] { ".colorIfFalseG", "colorIfFalseG", ".cfg", "cfg", ".colorIfFalseY", "colorIfFalseY" },
                zKeys: new[] { ".colorIfFalseB", "colorIfFalseB", ".cfb", "cfb", ".colorIfFalseZ", "colorIfFalseZ" }
            );

            alphaIfTrue = ReadFloat(1f, ".alphaIfTrue", "alphaIfTrue", ".at", "at", ".colorIfTrueA", "colorIfTrueA", ".cta", "cta");
            alphaIfFalse = ReadFloat(0f, ".alphaIfFalse", "alphaIfFalse", ".af", "af", ".colorIfFalseA", "colorIfFalseA", ".cfa", "cfa");

            incomingFirstTerm = FindLastIncomingTo("firstTerm", "ft");
            incomingSecondTerm = FindLastIncomingTo("secondTerm", "st");
            incomingColorIfTrue = FindLastIncomingTo("colorIfTrue", "colorIfTrueR", "colorIfTrueG", "colorIfTrueB", "ct", "ctr", "ctg", "ctb");
            incomingColorIfFalse = FindLastIncomingTo("colorIfFalse", "colorIfFalseR", "colorIfFalseG", "colorIfFalseB", "cf", "cfr", "cfg", "cfb");

            if (!enabled)
            {
                result = false;
                outColor = Vector3.zero;
                outAlpha = 0f;
                Publish();
                SetNotes($"{NodeType} '{NodeName}' disabled.");
                return;
            }

            result = Eval(operation, firstTerm, secondTerm);

            outColor = result ? colorIfTrue : colorIfFalse;
            outAlpha = result ? alphaIfTrue : alphaIfFalse;

            Publish();

            SetNotes(
                $"{NodeType} '{NodeName}' enabled op={operation} first={firstTerm:0.###} second={secondTerm:0.###} -> {result} " +
                $"outColor=({outColor.x:0.###},{outColor.y:0.###},{outColor.z:0.###}) outA={outAlpha:0.###} " +
                $"incoming(ft={(string.IsNullOrEmpty(incomingFirstTerm) ? "none" : incomingFirstTerm)}, st={(string.IsNullOrEmpty(incomingSecondTerm) ? "none" : incomingSecondTerm)})"
            );
        }

        private void Publish()
        {
            var v3 = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            v3.Set(MayaVector3Value.Kind.Vector, outColor, outColor);

            var f = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            f.Set(outAlpha);
        }

        // typical Maya condition operation mapping (best-effort):
        // 0 Equal, 1 NotEqual, 2 GreaterThan, 3 GreaterOrEqual, 4 LessThan, 5 LessOrEqual
        // handle extras safely:
        // 6 "Is True" (first != 0), 7 "Is False" (first == 0)
        private static bool Eval(int op, float a, float b)
        {
            const float eps = 1e-6f;

            switch (op)
            {
                case 1: return Mathf.Abs(a - b) > eps;
                case 2: return a > b;
                case 3: return a > b || Mathf.Abs(a - b) <= eps;
                case 4: return a < b;
                case 5: return a < b || Mathf.Abs(a - b) <= eps;
                case 6: return Mathf.Abs(a) > eps;
                case 7: return Mathf.Abs(a) <= eps;
                default: return Mathf.Abs(a - b) <= eps;
            }
        }

        private Vector3 ReadVec3(Vector3 def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            if (packedKeys != null)
            {
                for (int i = 0; i < packedKeys.Length; i++)
                {
                    var k = packedKeys[i];
                    if (string.IsNullOrEmpty(k)) continue;

                    if (TryGetTokens(k, out var t) && t != null && t.Count >= 3)
                    {
                        if (TryParseFloat(t[0], out var x) &&
                            TryParseFloat(t[1], out var y) &&
                            TryParseFloat(t[2], out var z))
                            return new Vector3(x, y, z);
                    }
                }
            }

            float xx = ReadFloat(def.x, xKeys);
            float yy = ReadFloat(def.y, yKeys);
            float zz = ReadFloat(def.z, zKeys);
            return new Vector3(xx, yy, zz);
        }

        private static bool TryParseFloat(string s, out float v)
        {
            return float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}


// ----------------------------------------------------------------------------- 
// INTEGRATED: ConditionEvalNode.cs
// -----------------------------------------------------------------------------
// PATCH: ProductionImpl v6 (Unity-only, retention-first)

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
