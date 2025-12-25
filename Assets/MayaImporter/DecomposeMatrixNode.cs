// Assets/MayaImporter/DecomposeMatrixNode.cs
// NodeType: decomposeMatrix
//
// Phase C: Implemented (best-effort reconstruction)
// - Resolves inputMatrix (incoming MayaMatrixValue preferred, else local setAttr matrix)
// - Decomposes into T/R/S via MatrixUtil.DecomposeTRS (best-effort)
// - Publishes outputs into metadata
// - Also publishes a MayaFloatValue (debug convenience)

using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [DisallowMultipleComponent]
    [MayaNodeType("decomposeMatrix")]
    public sealed class DecomposeMatrixNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (best-effort)")]
        [SerializeField] private string sourceSummary;
        [SerializeField] private Vector3 outTranslateMaya;
        [SerializeField] private Quaternion outRotateQuatMaya = Quaternion.identity;
        [SerializeField] private Vector3 outScaleMaya = Vector3.one;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaDecomposeMatrixMetadata>() ?? gameObject.AddComponent<MayaDecomposeMatrixMetadata>();
            meta.valid = false;

            Matrix4x4 mIn = Matrix4x4.identity;
            sourceSummary = "None";

            // 1) Connection preferred
            var incoming = FindLastIncomingTo("inputMatrix", "input", "inMatrix", "matrixIn");
            if (!string.IsNullOrEmpty(incoming))
            {
                var srcNode = MayaPlugUtil.ExtractNodePart(incoming);
                if (!string.IsNullOrEmpty(srcNode))
                {
                    var tr = MayaNodeLookup.FindTransform(srcNode);
                    if (tr != null)
                    {
                        var mv = tr.GetComponent<MayaMatrixValue>();
                        if (mv != null && mv.valid)
                        {
                            mIn = mv.mayaMatrix;
                            sourceSummary = $"Conn:{incoming}";
                        }
                    }
                }
            }

            // 2) Local attribute fallback
            if (sourceSummary == "None")
            {
                if (!TryReadMatrix4x4(".inputMatrix", out mIn) &&
                    !TryReadMatrix4x4("inputMatrix", out mIn) &&
                    !TryReadMatrix4x4(".input", out mIn) &&
                    !TryReadMatrix4x4("input", out mIn))
                {
                    mIn = Matrix4x4.identity;
                }
                sourceSummary = "LocalAttr";
            }

            // Store input
            meta.source = sourceSummary;
            meta.inputMatrixMaya = mIn;
            meta.inputMatrixUnity = MayaToUnityConversion.ConvertMatrix(mIn, options.Conversion);

            // Decompose in Maya space
            MatrixUtil.DecomposeTRS(mIn, out var tMaya, out var rMaya, out var sMaya);

            outTranslateMaya = tMaya;
            outRotateQuatMaya = rMaya;
            outScaleMaya = sMaya;

            meta.outputTranslateMaya = tMaya;
            meta.outputRotateQuatMaya = rMaya;
            meta.outputScaleMaya = sMaya;

            // Unity best-effort: rotationは ConvertMatrix(Rotate(q)).rotation で取得（ConvertRotation不要）
            meta.outputTranslateUnity = MayaToUnityConversion.ConvertPosition(tMaya, options.Conversion);
            meta.outputRotateQuatUnity = MayaToUnityConversion.ConvertMatrix(Matrix4x4.Rotate(rMaya), options.Conversion).rotation;
            meta.outputScaleUnity = sMaya;

            // Debug scalar carrier
            var fv = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            fv.Set(tMaya.magnitude);

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[decomposeMatrix] '{NodeName}' src={sourceSummary} t=({tMaya.x:0.###},{tMaya.y:0.###},{tMaya.z:0.###}) s=({sMaya.x:0.###},{sMaya.y:0.###},{sMaya.z:0.###})");
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaDecomposeMatrixMetadata : MonoBehaviour
    {
        public bool valid;

        [Header("Input")]
        public string source;
        public Matrix4x4 inputMatrixMaya = Matrix4x4.identity;
        public Matrix4x4 inputMatrixUnity = Matrix4x4.identity;

        [Header("Output (Maya)")]
        public Vector3 outputTranslateMaya;
        public Quaternion outputRotateQuatMaya = Quaternion.identity;
        public Vector3 outputScaleMaya = Vector3.one;

        [Header("Output (Unity best-effort)")]
        public Vector3 outputTranslateUnity;
        public Quaternion outputRotateQuatUnity = Quaternion.identity;
        public Vector3 outputScaleUnity = Vector3.one;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
