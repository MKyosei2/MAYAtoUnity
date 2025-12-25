// Assets/MayaImporter/MayaGenerated_PickMatrixNode.cs
// NodeType: pickMatrix
//
// Phase C: Implemented (best-effort reconstruction)
//
// - Reads inputMatrix (incoming MayaMatrixValue preferred, else local setAttr matrix)
// - Applies channel masks (useTranslate / useRotate / useScale; shear is ignored best-effort)
// - Re-composes matrix (Maya space) and publishes MayaMatrixValue
//
// Notes:
// - pickMatrix in Maya can preserve shear/pivots/rotateAxis in some cases.
//   This importer focuses on robust TRS reconstruction for Unity-side rigs.
// - When useShear is false/true, this implementation behaves the same (shear is not reconstructed).

using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("pickMatrix")]
    public sealed class MayaGenerated_PickMatrixNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (pickMatrix)")]
        [SerializeField] private bool enabled = true;

        [Header("Mask flags")]
        [SerializeField] private bool useTranslate = true;
        [SerializeField] private bool useRotate = true;
        [SerializeField] private bool useScale = true;
        [SerializeField] private bool useShear = true;

        [Header("Sources")]
        [SerializeField] private string incomingInputMatrix;

        [Header("Outputs (best-effort)")]
        [SerializeField] private Matrix4x4 inputMatrixMaya = Matrix4x4.identity;
        [SerializeField] private Matrix4x4 outputMatrixMaya = Matrix4x4.identity;
        [SerializeField] private Matrix4x4 outputMatrixUnity = Matrix4x4.identity;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            // Flags (names are fairly stable, but we accept a few aliases)
            useTranslate = ReadBool(true, ".useTranslate", "useTranslate", ".ut", "ut");
            useRotate = ReadBool(true, ".useRotate", "useRotate", ".ur", "ur");
            useScale = ReadBool(true, ".useScale", "useScale", ".us", "us");
            useShear = ReadBool(true, ".useShear", "useShear", ".ush", "ush");

            incomingInputMatrix = NormalizePlug(FindLastIncomingTo("inputMatrix", "input", "inMatrix", "matrixIn"));

            // Resolve input matrix
            if (!string.IsNullOrEmpty(incomingInputMatrix) && TryResolveConnectedMatrix(incomingInputMatrix, out var mConn))
            {
                inputMatrixMaya = mConn;
            }
            else
            {
                // local matrix (best-effort)
                if (!TryReadMatrix4x4(".inputMatrix", out inputMatrixMaya) &&
                    !TryReadMatrix4x4("inputMatrix", out inputMatrixMaya) &&
                    !TryReadMatrix4x4(".input", out inputMatrixMaya) &&
                    !TryReadMatrix4x4("input", out inputMatrixMaya))
                {
                    inputMatrixMaya = Matrix4x4.identity;
                }
            }

            // Decompose
            MatrixUtil.DecomposeTRS(inputMatrixMaya, out var t, out var r, out var s);

            if (!useTranslate) t = Vector3.zero;
            if (!useRotate) r = Quaternion.identity;
            if (!useScale) s = Vector3.one;

            // Shear is ignored (best-effort)
            outputMatrixMaya = MatrixUtil.ComposeTRS(t, r, s);
            outputMatrixUnity = MayaToUnityConversion.ConvertMatrix(outputMatrixMaya, options.Conversion);

            var outVal = GetComponent<MayaMatrixValue>() ?? gameObject.AddComponent<MayaMatrixValue>();
            outVal.valid = true;
            outVal.mayaMatrix = outputMatrixMaya;
            outVal.unityMatrix = outputMatrixUnity;
            outVal.matrixMaya = outVal.mayaMatrix;
            outVal.matrixUnity = outVal.unityMatrix;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, " +
                     $"useT={useTranslate}, useR={useRotate}, useS={useScale}, useSh={useShear}, " +
                     $"src={(string.IsNullOrEmpty(incomingInputMatrix) ? "LocalAttr" : incomingInputMatrix)} " +
                     $"(published MayaMatrixValue)");
        }

        private bool TryResolveConnectedMatrix(string srcPlug, out Matrix4x4 mayaMatrix)
        {
            mayaMatrix = Matrix4x4.identity;
            if (string.IsNullOrEmpty(srcPlug)) return false;

            var node = MayaPlugUtil.ExtractNodePart(srcPlug);
            if (string.IsNullOrEmpty(node)) return false;

            var tr = MayaNodeLookup.FindTransform(node);
            if (tr == null) return false;

            var mv = tr.GetComponent<MayaMatrixValue>();
            if (mv != null && mv.valid)
            {
                mayaMatrix = mv.mayaMatrix;
                return true;
            }

            return false;
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
