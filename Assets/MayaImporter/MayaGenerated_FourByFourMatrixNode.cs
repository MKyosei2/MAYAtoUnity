// PATCH: ProductionImpl v6 (Unity-only, retention-first)
﻿// Assets/MayaImporter/MayaGenerated_FourByFourMatrixNode.cs
// NodeType: fourByFourMatrix
//
// Phase C: Implemented (best-effort reconstruction)
//
// - Reads 16 scalar inputs in00..in33 (connections preferred, else local setAttr)
// - Builds Maya-space Matrix4x4 (row-major mapping to m00..m33)
// - Publishes output via MayaImporter.Core.MayaMatrixValue
//
// Notes:
// - Maya has many matrix conventions; this node simply assembles the raw 4x4 as authored.
// - Coordinate conversion (Maya->Unity) is handled by MayaToUnityConversion.

using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("fourByFourMatrix")]
    public sealed class MayaGenerated_FourByFourMatrixNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (fourByFourMatrix)")]
        [SerializeField] private bool enabled = true;

        // Stored in row-major order: r*4+c
        [SerializeField] private float[] inputs = new float[16];
        [SerializeField] private string[] srcPlugs = new string[16];

        [Header("Outputs (best-effort)")]
        [SerializeField] private Matrix4x4 outputMatrixMaya = Matrix4x4.identity;
        [SerializeField] private Matrix4x4 outputMatrixUnity = Matrix4x4.identity;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            EnsureArrays();

            // Read 16 cells
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int idx = r * 4 + c;
                    string attr = $"in{r}{c}";

                    // Connection preferred
                    srcPlugs[idx] = NormalizePlug(FindLastIncomingTo(attr));
                    float local = ReadFloat(0f, "." + attr, attr);

                    inputs[idx] = ResolveConnectedFloat(srcPlugs[idx], local);
                }
            }

            // Build Maya matrix
            outputMatrixMaya = new Matrix4x4
            {
                m00 = inputs[0],
                m01 = inputs[1],
                m02 = inputs[2],
                m03 = inputs[3],
                m10 = inputs[4],
                m11 = inputs[5],
                m12 = inputs[6],
                m13 = inputs[7],
                m20 = inputs[8],
                m21 = inputs[9],
                m22 = inputs[10],
                m23 = inputs[11],
                m30 = inputs[12],
                m31 = inputs[13],
                m32 = inputs[14],
                m33 = inputs[15],
            };

            outputMatrixUnity = MayaToUnityConversion.ConvertMatrix(outputMatrixMaya, options.Conversion);

            var outVal = GetComponent<MayaMatrixValue>() ?? gameObject.AddComponent<MayaMatrixValue>();
            outVal.valid = true;

            // Set both canonical + alias (for compatibility)
            outVal.mayaMatrix = outputMatrixMaya;
            outVal.unityMatrix = outputMatrixUnity;
            outVal.matrixMaya = outVal.mayaMatrix;
            outVal.matrixUnity = outVal.unityMatrix;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, " +
                     $"m03/m13/m23=({outputMatrixMaya.m03:0.###},{outputMatrixMaya.m13:0.###},{outputMatrixMaya.m23:0.###}) " +
                     $"(published MayaMatrixValue)");
        }

        private void EnsureArrays()
        {
            if (inputs == null || inputs.Length != 16) inputs = new float[16];
            if (srcPlugs == null || srcPlugs.Length != 16) srcPlugs = new string[16];
        }

        private float ResolveConnectedFloat(string srcPlug, float fallback)
        {
            if (string.IsNullOrEmpty(srcPlug)) return fallback;

            var node = MayaPlugUtil.ExtractNodePart(srcPlug);
            if (string.IsNullOrEmpty(node)) return fallback;

            var tr = MayaNodeLookup.FindTransform(node);
            if (tr == null) return fallback;

            var fv = tr.GetComponent<MayaFloatValue>();
            if (fv != null && fv.valid) return fv.value;

            // Some legacy nodes may still use MayaImporter.Nodes.MayaFloatValue
            var fv2 = tr.GetComponent<MayaImporter.Nodes.MayaFloatValue>();
            if (fv2 != null && fv2.valid) return fv2.value;

            return fallback;
        }

        private static string NormalizePlug(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            plug = plug.Trim();
            if (plug.Length >= 2 && plug[0] == '"' && plug[plug.Length - 1] == '"')
                plug = plug.Substring(1, plug.Length - 2);
            return plug;
        }
    }
}
