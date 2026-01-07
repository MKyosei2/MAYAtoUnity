// Assets/MayaImporter/MultMatrixNode.cs
// Production implementation
//
// NodeType: multMatrix
// - Collects matrixIn[i] from setAttr and from incoming connections (best-effort)
// - Computes output = �� matrixIn[i] in ascending index order
// - Publishes output via MayaImporter.Core.MayaMatrixValue

using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Nodes
{
    [Serializable]
    public struct MayaIndexedMatrix
    {
        public int index;
        public string source;
        public Matrix4x4 mayaMatrix;
    }

    [MayaNodeType("multMatrix")]
    [DisallowMultipleComponent]
    public sealed class MultMatrixNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            var meta = GetComponent<MayaMultMatrixMetadata>() ?? gameObject.AddComponent<MayaMultMatrixMetadata>();
            meta.valid = false;

            var outVal = GetComponent<MayaImporter.Core.MayaMatrixValue>() ?? gameObject.AddComponent<MayaImporter.Core.MayaMatrixValue>();
            outVal.valid = false;

            // Gather from attributes
            meta.inputs.Clear();

            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null) continue;

                    var key = a.Key ?? "";
                    if (!LooksLikeMatrixInKey(key)) continue;

                    int idx = ExtractBracketIndexOrMinus1(key);
                    if (idx < 0) idx = 0;

                    if (a.Tokens == null || a.Tokens.Count < 16) continue;
                    if (!MatrixUtil.TryParseMatrix4x4(a.Tokens, 0, out var m)) continue;

                    meta.inputs.Add(new MayaIndexedMatrix
                    {
                        index = idx,
                        source = $"LocalAttr:{a.Key}",
                        mayaMatrix = m
                    });
                }
            }

            // Gather from incoming connections (preferred)
            GatherIncomingMatrices(meta.inputs);

            // Sort deterministically
            meta.inputs.Sort((x, y) => x.index.CompareTo(y.index));

            // Multiply in ascending order
            Matrix4x4 result = Matrix4x4.identity;

            var inputs = meta.inputs;
            for (int i = 0; i < inputs.Count; i++)
                result = result * inputs[i].mayaMatrix;

            meta.outputMatrixMaya = result;
            meta.outputMatrixUnity = MayaToUnityConversion.ConvertMatrix(result, options.Conversion);

            outVal.valid = true;
            outVal.mayaMatrix = meta.outputMatrixMaya;
            outVal.unityMatrix = meta.outputMatrixUnity;
            outVal.matrixMaya = outVal.mayaMatrix;
            outVal.matrixUnity = outVal.unityMatrix;

            meta.valid = true;
            meta.lastBuildFrame = Time.frameCount;

            log.Info($"[multMatrix] '{NodeName}' inputs={inputs.Count} => t=({result.m03:0.###},{result.m13:0.###},{result.m23:0.###})");
        }

        private void GatherIncomingMatrices(List<MayaIndexedMatrix> list)
        {
            if (Connections == null || Connections.Count == 0) return;

            // multMatrix uses matrixIn[0..] typically
            // We scan destination plugs for ".matrixIn[" and resolve from source MayaMatrixValue if present.
            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                if (!dstAttr.StartsWith("matrixIn[", StringComparison.Ordinal) &&
                    !dstAttr.StartsWith("matrix[", StringComparison.Ordinal))
                    continue;

                int idx = ExtractBracketIndexOrMinus1(dstAttr);
                if (idx < 0) idx = 0;

                var srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (string.IsNullOrEmpty(srcNode)) continue;

                var tr = MayaNodeLookup.FindTransform(srcNode);
                if (tr == null) continue;

                var mv = tr.GetComponent<MayaMatrixValue>();
                if (mv == null || !mv.valid) continue;

                // override any existing local with same idx
                RemoveIndex(list, idx);

                list.Add(new MayaIndexedMatrix
                {
                    index = idx,
                    source = $"Conn:{c.SrcPlug}",
                    mayaMatrix = mv.mayaMatrix
                });
            }
        }

        private static void RemoveIndex(List<MayaIndexedMatrix> list, int idx)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].index == idx)
                    list.RemoveAt(i);
            }
        }

        private static bool LooksLikeMatrixInKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            // accept "matrixIn[0]" or ".matrixIn[0]" or "matrix[0]"
            return key.StartsWith("matrixIn[", StringComparison.Ordinal) ||
                   key.StartsWith(".matrixIn[", StringComparison.Ordinal) ||
                   key.StartsWith("matrix[", StringComparison.Ordinal) ||
                   key.StartsWith(".matrix[", StringComparison.Ordinal);
        }

        private static int ExtractBracketIndexOrMinus1(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            int lb = s.IndexOf('[');
            int rb = s.IndexOf(']');
            if (lb < 0 || rb < 0 || rb <= lb + 1) return -1;
            var inner = s.Substring(lb + 1, rb - lb - 1);
            if (int.TryParse(inner, out int idx)) return idx;
            return -1;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaMultMatrixMetadata : MonoBehaviour
    {
        public bool valid;

        [Header("Inputs (collected, Maya space)")]
        public List<MayaIndexedMatrix> inputs = new List<MayaIndexedMatrix>();

        [Header("Outputs (best-effort)")]
        public Matrix4x4 outputMatrixMaya = Matrix4x4.identity;
        public Matrix4x4 outputMatrixUnity = Matrix4x4.identity;

        [Header("Debug")]
        public int lastBuildFrame;
    }
}
