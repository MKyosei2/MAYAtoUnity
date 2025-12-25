using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Nodes
{
    [MayaNodeType("transposeMatrix")]
    [DisallowMultipleComponent]
    public sealed class TransposeMatrixNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (transposeMatrix) - local preview")]
        [SerializeField] private bool hasLocalInputMatrix;
        [SerializeField] private Matrix4x4 inputMatrix = Matrix4x4.identity;
        [SerializeField] private Matrix4x4 transposed = Matrix4x4.identity;

        [SerializeField] private string inputIncomingPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // Common attr name candidates
            hasLocalInputMatrix =
                TryReadMatrix4x4(".inputMatrix", out inputMatrix) ||
                TryReadMatrix4x4("inputMatrix", out inputMatrix) ||
                TryReadMatrix4x4(".inMatrix", out inputMatrix) ||
                TryReadMatrix4x4("inMatrix", out inputMatrix) ||
                TryReadMatrix4x4(".matrix", out inputMatrix) ||
                TryReadMatrix4x4("matrix", out inputMatrix);

            inputIncomingPlug = FindLastIncomingTo("inputMatrix", "inMatrix", "matrix");

            if (hasLocalInputMatrix)
            {
                transposed = inputMatrix.transpose;
                SetNotes($"transposeMatrix decoded: local inputMatrix found; transposed preview computed. incoming={(string.IsNullOrEmpty(inputIncomingPlug) ? "none" : inputIncomingPlug)}");
            }
            else
            {
                transposed = Matrix4x4.identity;
                SetNotes($"transposeMatrix decoded: no local matrix tokens (likely connection-driven). incoming={(string.IsNullOrEmpty(inputIncomingPlug) ? "none" : inputIncomingPlug)}");
            }
        }
    }
}
