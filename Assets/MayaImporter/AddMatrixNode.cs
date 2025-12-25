// Assets/MayaImporter/AddMatrixNode.cs
// Phase C placeholder -> implemented
// NodeType: addMatrix
//
// - Collect input matrices from attributes (matrixIn[...] / inputMatrix[...] / inMatrix[...] ÇçLÇ≠èEÇ§)
// - Best-effort: output = É∞ inputMatrix[i] (element-wise)
// - Publishes output via MayaImporter.Core.MayaMatrixValue
//
// IMPORTANT:
// Do NOT define MayaMatrixValue in this file. Use Core.MayaMatrixValue only.

using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [MayaNodeType("addMatrix")]
    [DisallowMultipleComponent]
    public sealed class AddMatrixNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaAddMatrixMetadata>() ?? gameObject.AddComponent<MayaAddMatrixMetadata>();
            meta.valid = false;

            var outVal = GetComponent<MayaMatrixValue>() ?? gameObject.AddComponent<MayaMatrixValue>();
            outVal.valid = false;

            // 1) gather inputs from raw attributes
            var inputs = new List<MayaMatrixInput>(8);

            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null) continue;

                    var key = a.Key ?? "";
                    if (key.Length == 0) continue;

                    if (!LooksLikeMatrixInputKey(key))
                        continue;

                    if (a.Tokens == null || a.Tokens.Count < 16)
                        continue;

                    if (!MatrixUtil.TryParseMatrix4x4(a.Tokens, 0, out var m))
                        continue;

                    inputs.Add(new MayaMatrixInput
                    {
                        key = key,
                        matrixMaya = m
                    });
                }
            }

            meta.inputs = inputs;

            // 2) best-effort sum
            var sum = Matrix4x4.zero;
            if (inputs.Count == 0)
            {
                // fallback: some scenes store single ".matrix" / ".inputMatrix"
                sum = ReadMatrixOrIdentity(".inputMatrix", "inputMatrix", ".matrix", "matrix");
            }
            else
            {
                for (int i = 0; i < inputs.Count; i++)
                    sum = Add(sum, inputs[i].matrixMaya);
            }

            meta.outputMatrixMaya = sum;
            meta.outputMatrixUnity = MayaToUnityConversion.ConvertMatrix(sum, options.Conversion);

            outVal.valid = true;
            // write to canonical + alias (Core class handles alias properties)
            outVal.matrixMaya = meta.outputMatrixMaya;
            outVal.matrixUnity = meta.outputMatrixUnity;

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[addMatrix] '{NodeName}' inputs={inputs.Count} out(Maya) m03={sum.m03:0.###},m13={sum.m13:0.###},m23={sum.m23:0.###}");
        }

        private static bool LooksLikeMatrixInputKey(string key)
        {
            return key.Contains("matrixIn[", StringComparison.Ordinal) ||
                   key.Contains("inputMatrix[", StringComparison.Ordinal) ||
                   key.Contains("inMatrix[", StringComparison.Ordinal) ||
                   key.Contains(".matrixIn[", StringComparison.Ordinal) ||
                   key.Contains(".inputMatrix[", StringComparison.Ordinal) ||
                   key.Contains(".inMatrix[", StringComparison.Ordinal);
        }

        private Matrix4x4 ReadMatrixOrIdentity(params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetAttr(keys[i], out var a) && a.Tokens != null && a.Tokens.Count >= 16)
                {
                    if (MatrixUtil.TryParseMatrix4x4(a.Tokens, 0, out var m))
                        return m;
                }
            }
            return Matrix4x4.identity;
        }

        private static Matrix4x4 Add(in Matrix4x4 a, in Matrix4x4 b)
        {
            Matrix4x4 r = new Matrix4x4();
            r.m00 = a.m00 + b.m00; r.m01 = a.m01 + b.m01; r.m02 = a.m02 + b.m02; r.m03 = a.m03 + b.m03;
            r.m10 = a.m10 + b.m10; r.m11 = a.m11 + b.m11; r.m12 = a.m12 + b.m12; r.m13 = a.m13 + b.m13;
            r.m20 = a.m20 + b.m20; r.m21 = a.m21 + b.m21; r.m22 = a.m22 + b.m22; r.m23 = a.m23 + b.m23;
            r.m30 = a.m30 + b.m30; r.m31 = a.m31 + b.m31; r.m32 = a.m32 + b.m32; r.m33 = a.m33 + b.m33;
            return r;
        }
    }

    [Serializable]
    public struct MayaMatrixInput
    {
        public string key;
        public Matrix4x4 matrixMaya;
    }

    [DisallowMultipleComponent]
    public sealed class MayaAddMatrixMetadata : MonoBehaviour
    {
        public bool valid;

        [Header("Collected inputs (from setAttr)")]
        public List<MayaMatrixInput> inputs = new List<MayaMatrixInput>();

        [Header("Output (best-effort)")]
        public Matrix4x4 outputMatrixMaya = Matrix4x4.identity;
        public Matrix4x4 outputMatrixUnity = Matrix4x4.identity;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
