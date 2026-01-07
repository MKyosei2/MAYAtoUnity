// MayaImporter/VectorProductNode.cs
// NodeType: vectorProduct
// Production (real decode + value publish)
//
// Publishes:
// - MayaVector3Value : output vector/point (best-effort)
// - MayaFloatValue   : scalar convenience (dot or magnitude)
//
// Operation (best-effort typical):
// 0 Dot product        -> output = (dot, 0, 0) / scalar=dot
// 1 Cross product      -> output = cross / scalar=|cross|
// 2 Vector * Matrix    -> output = M * v (MultiplyVector) / scalar=|out|
// 3 Point  * Matrix    -> output = M * p (MultiplyPoint3x4) / scalar=|out|
//
// Notes:
// - Works without Maya/API. Uses stored setAttr tokens + connectAttr hints only.

using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    [DisallowMultipleComponent]
    [MayaNodeType("vectorProduct")]
    public sealed class VectorProductNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (vectorProduct)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private int operation = 0;

        [SerializeField] private bool normalizeInput1 = false;
        [SerializeField] private bool normalizeInput2 = false;
        [SerializeField] private bool normalizeOutput = false;

        [SerializeField] private Vector3 input1 = Vector3.zero;
        [SerializeField] private Vector3 input2 = Vector3.zero;

        [SerializeField] private bool hasMatrix = false;
        [SerializeField] private Matrix4x4 matrix = Matrix4x4.identity;

        [Header("Incoming (best-effort)")]
        [SerializeField] private string incomingInput1;
        [SerializeField] private string incomingInput2;
        [SerializeField] private string incomingMatrix;

        [Header("Output (computed)")]
        [SerializeField] private MayaVector3Value.Kind outKind = MayaVector3Value.Kind.Vector;
        [SerializeField] private Vector3 outValue = Vector3.zero;
        [SerializeField] private float outScalar = 0f;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // enable
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            operation = ReadInt(0, ".operation", "operation", ".op", "op");

            normalizeInput1 = ReadBool(false, ".normalizeInput1", "normalizeInput1", ".ni1", "ni1");
            normalizeInput2 = ReadBool(false, ".normalizeInput2", "normalizeInput2", ".ni2", "ni2");
            normalizeOutput = ReadBool(false, ".normalizeOutput", "normalizeOutput", ".no", "no", ".normalize", "normalize");

            input1 = ReadVec3(
                def: Vector3.zero,
                packedKeys: new[] { ".input1", "input1", ".i1", "i1" },
                xKeys: new[] { ".input1X", "input1X", ".i1x", "i1x" },
                yKeys: new[] { ".input1Y", "input1Y", ".i1y", "i1y" },
                zKeys: new[] { ".input1Z", "input1Z", ".i1z", "i1z" }
            );

            input2 = ReadVec3(
                def: Vector3.zero,
                packedKeys: new[] { ".input2", "input2", ".i2", "i2" },
                xKeys: new[] { ".input2X", "input2X", ".i2x", "i2x" },
                yKeys: new[] { ".input2Y", "input2Y", ".i2y", "i2y" },
                zKeys: new[] { ".input2Z", "input2Z", ".i2z", "i2z" }
            );

            hasMatrix =
                TryReadMatrix4x4(".matrix", out matrix) ||
                TryReadMatrix4x4("matrix", out matrix) ||
                TryReadMatrix4x4(".inputMatrix", out matrix) ||
                TryReadMatrix4x4("inputMatrix", out matrix) ||
                TryReadMatrix4x4(".mat", out matrix) ||
                TryReadMatrix4x4("mat", out matrix);

            // connection hints
            incomingInput1 = FindLastIncomingTo("input1", "input1X", "input1Y", "input1Z", "i1", "i1x", "i1y", "i1z");
            incomingInput2 = FindLastIncomingTo("input2", "input2X", "input2Y", "input2Z", "i2", "i2x", "i2y", "i2z");
            incomingMatrix = FindLastIncomingTo("matrix", "inputMatrix", "mat");

            // compute
            outKind = MayaVector3Value.Kind.Vector;
            outValue = Vector3.zero;
            outScalar = 0f;

            if (!enabled)
            {
                Publish();
                SetNotes($"{NodeType} '{NodeName}' disabled. (generic attrs kept)");
                return;
            }

            Vector3 a = input1;
            Vector3 b = input2;

            if (normalizeInput1 && a.sqrMagnitude > 1e-12f) a = a.normalized;
            if (normalizeInput2 && b.sqrMagnitude > 1e-12f) b = b.normalized;

            switch (operation)
            {
                case 1: // cross
                    outValue = Vector3.Cross(a, b);
                    outScalar = outValue.magnitude;
                    outKind = MayaVector3Value.Kind.Vector;
                    break;

                case 2: // vector * matrix (3x3)
                    outValue = hasMatrix ? matrix.MultiplyVector(a) : a;
                    outScalar = outValue.magnitude;
                    outKind = MayaVector3Value.Kind.Vector;
                    break;

                case 3: // point * matrix
                    outValue = hasMatrix ? matrix.MultiplyPoint3x4(a) : a;
                    outScalar = outValue.magnitude;
                    outKind = MayaVector3Value.Kind.Point;
                    break;

                case 0: // dot
                default:
                    outScalar = Vector3.Dot(a, b);
                    // Mayaでは dot は outputX を使うケースが多いので (dot,0,0) に寄せる
                    outValue = new Vector3(outScalar, 0f, 0f);
                    outKind = MayaVector3Value.Kind.Vector;
                    break;
            }

            if (normalizeOutput)
            {
                // dotの場合は scalar が主で、正規化は期待値がぶれやすいので vector出力だけ正規化
                if (operation != 0 && outValue.sqrMagnitude > 1e-12f)
                    outValue = outValue.normalized;
            }

            Publish();

            SetNotes(
                $"{NodeType} '{NodeName}' enabled op={operation} ni1={normalizeInput1} ni2={normalizeInput2} no={normalizeOutput} " +
                $"in1=({input1.x:0.###},{input1.y:0.###},{input1.z:0.###}) in2=({input2.x:0.###},{input2.y:0.###},{input2.z:0.###}) " +
                $"hasMat={hasMatrix} out=({outValue.x:0.###},{outValue.y:0.###},{outValue.z:0.###}) scalar={outScalar:0.###} " +
                $"incoming(i1={(string.IsNullOrEmpty(incomingInput1) ? "none" : incomingInput1)}, i2={(string.IsNullOrEmpty(incomingInput2) ? "none" : incomingInput2)}, mat={(string.IsNullOrEmpty(incomingMatrix) ? "none" : incomingMatrix)})"
            );
        }

        private void Publish()
        {
            var v = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            v.Set(outKind, outValue, outValue);

            var s = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            s.Set(outScalar);
        }

        private Vector3 ReadVec3(Vector3 def, string[] packedKeys, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            // packed: 3 tokens (values only; -typeは別に保持されるので混ざらない)
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
