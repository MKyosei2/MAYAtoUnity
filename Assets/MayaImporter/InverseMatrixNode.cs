// Assets/MayaImporter/InverseMatrixNode.cs
// Production implementation
//
// NodeType: inverseMatrix
// - Reads inputMatrix from setAttr or incoming connection (best-effort)
// - output = inverse(input)
// - Publishes output via MayaImporter.Core.MayaMatrixValue

using System;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [MayaNodeType("inverseMatrix")]
    [DisallowMultipleComponent]
    public sealed class InverseMatrixNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaInverseMatrixMetadata>() ?? gameObject.AddComponent<MayaInverseMatrixMetadata>();
            meta.valid = false;

            var outVal = GetComponent<MayaImporter.Core.MayaMatrixValue>() ?? gameObject.AddComponent<MayaImporter.Core.MayaMatrixValue>();
            outVal.valid = false;

            // Try incoming first (best-effort)
            if (!TryResolveIncomingMatrix(out var mIn, out var srcSummary))
            {
                // Fallback: local attribute
                mIn = ReadMatrixOrIdentity(
                    ".inputMatrix", "inputMatrix",
                    ".inMatrix", "inMatrix",
                    ".matrix", "matrix",
                    ".im", "im"
                );
                srcSummary = "LocalAttr";
            }

            meta.source = srcSummary;
            meta.inputMatrixMaya = mIn;

            var inv = SafeInverse(mIn);
            meta.outputMatrixMaya = inv;
            meta.outputMatrixUnity = MayaToUnityConversion.ConvertMatrix(inv, options.Conversion);

            outVal.valid = true;
            outVal.mayaMatrix = meta.outputMatrixMaya;
            outVal.unityMatrix = meta.outputMatrixUnity;
            outVal.matrixMaya = outVal.mayaMatrix;
            outVal.matrixUnity = outVal.unityMatrix;

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[inverseMatrix] '{NodeName}' src='{meta.source}' out(Maya) t=({inv.m03:0.###},{inv.m13:0.###},{inv.m23:0.###})");
        }

        private bool TryResolveIncomingMatrix(out Matrix4x4 m, out string srcSummary)
        {
            m = Matrix4x4.identity;
            srcSummary = "None";

            if (Connections == null || Connections.Count == 0)
                return false;

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstPlug ?? "";
                if (!dst.Contains("inputMatrix", StringComparison.Ordinal) &&
                    !dst.Contains("inMatrix", StringComparison.Ordinal) &&
                    !dst.EndsWith(".im", StringComparison.Ordinal))
                    continue;

                var srcNode = c.SrcNodePart;
                if (string.IsNullOrEmpty(srcNode))
                    srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);

                if (string.IsNullOrEmpty(srcNode))
                    continue;

                var tr = MayaNodeLookup.FindTransform(srcNode);
                if (tr == null) continue;

                var mv = tr.GetComponent<MayaImporter.Core.MayaMatrixValue>();
                if (mv != null && mv.valid)
                {
                    m = mv.mayaMatrix;
                    srcSummary = $"Incoming:{srcNode}";
                    return true;
                }

                srcSummary = $"Incoming:{srcNode}(no MayaMatrixValue)";
            }

            return false;
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

        private static Matrix4x4 SafeInverse(in Matrix4x4 m)
        {
            // Matrix4x4.inverse is deterministic; if non-invertible it returns something (may contain inf/nan).
            // We guard by determinant-ish check using reciprocal condition of columns.
            // If it looks singular, return identity.
            float detApprox =
                m.m00 * (m.m11 * m.m22 - m.m12 * m.m21) -
                m.m01 * (m.m10 * m.m22 - m.m12 * m.m20) +
                m.m02 * (m.m10 * m.m21 - m.m11 * m.m20);

            if (Mathf.Abs(detApprox) < 1e-12f)
                return Matrix4x4.identity;

            return m.inverse;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaInverseMatrixMetadata : MonoBehaviour
    {
        public bool valid;

        [Header("Source")]
        public string source;

        [Header("Maya values (best-effort)")]
        public Matrix4x4 inputMatrixMaya = Matrix4x4.identity;
        public Matrix4x4 outputMatrixMaya = Matrix4x4.identity;
        public Matrix4x4 outputMatrixUnity = Matrix4x4.identity;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
