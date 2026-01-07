// PATCH: ProductionImpl v6 (Unity-only, retention-first)
﻿// Assets/MayaImporter/MayaGenerated_WtAddMatrixNode.cs
// NodeType: wtAddMatrix (PhaseC implemented)
// - Collect wtMatrix[i].matrixIn + wtMatrix[i].weightIn
// - Best-effort output as TRS weighted average (deterministic)
// - Publishes MayaMatrixValue (matrixMaya/matrixUnity)

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;
using MayaImporter.Utils;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("wtAddMatrix")]
    public sealed class MayaGenerated_WtAddMatrixNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (wtAddMatrix)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private int termCount;
        [SerializeField] private int[] indices;
        [SerializeField] private float[] weightsLocal;
        [SerializeField] private string[] srcMatrixPlugs;
        [SerializeField] private string[] srcWeightPlugs;

        [Header("Output (best-effort)")]
        [SerializeField] private float weightSum;
        [SerializeField] private Matrix4x4 outputMatrixMaya = Matrix4x4.identity;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            var idx = CollectWtMatrixIndices();
            if (idx.Count == 0)
            {
                termCount = 0;
                indices = Array.Empty<int>();
                weightsLocal = Array.Empty<float>();
                srcMatrixPlugs = Array.Empty<string>();
                srcWeightPlugs = Array.Empty<string>();

                weightSum = 0f;
                outputMatrixMaya = Matrix4x4.identity;

                PublishMatrix(options, outputMatrixMaya);

                SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, terms=0 => identity");
                return;
            }

            idx.Sort();
            indices = idx.ToArray();
            termCount = indices.Length;

            weightsLocal = new float[termCount];
            srcMatrixPlugs = new string[termCount];
            srcWeightPlugs = new string[termCount];

            // Accumulators
            Vector3 tSum = Vector3.zero;
            Vector3 sSum = Vector3.zero;

            Quaternion qRef = Quaternion.identity;
            Vector4 qAcc = Vector4.zero;

            weightSum = 0f;

            bool hasAny = false;

            for (int i = 0; i < termCount; i++)
            {
                int id = indices[i];

                float wLocal = ReadFloat(1f,
                    $".wtMatrix[{id}].weightIn", $"wtMatrix[{id}].weightIn",
                    $".wtMatrix[{id}].weight", $"wtMatrix[{id}].weight",
                    $".wtMatrix[{id}].w", $"wtMatrix[{id}].w");

                weightsLocal[i] = wLocal;

                srcMatrixPlugs[i] = FindIncomingToExact(
                    $"wtMatrix[{id}].matrixIn",
                    $"wtMatrix[{id}].matrix",
                    $"wtMatrix[{id}].m");

                srcWeightPlugs[i] = FindIncomingToExact(
                    $"wtMatrix[{id}].weightIn",
                    $"wtMatrix[{id}].weight",
                    $"wtMatrix[{id}].w");

                // Resolve weight: connection > local
                float w = ResolveConnectedFloat(srcWeightPlugs[i], wLocal);

                // Resolve matrix: connection > local
                Matrix4x4 m = ResolveConnectedMatrix(srcMatrixPlugs[i], out bool gotConn);
                if (!gotConn)
                {
                    if (!TryReadMatrix4x4($".wtMatrix[{id}].matrixIn", out m) &&
                        !TryReadMatrix4x4($"wtMatrix[{id}].matrixIn", out m) &&
                        !TryReadMatrix4x4($".wtMatrix[{id}].matrix", out m) &&
                        !TryReadMatrix4x4($"wtMatrix[{id}].matrix", out m))
                    {
                        m = Matrix4x4.identity;
                    }
                }

                if (Mathf.Abs(w) <= 1e-8f) continue;

                hasAny = true;
                weightSum += w;

                MatrixUtil.DecomposeTRS(m, out var t, out var r, out var s);

                tSum += t * w;
                sSum += s * w;

                if (qAcc == Vector4.zero)
                {
                    qRef = r;
                }
                else
                {
                    if (Quaternion.Dot(qRef, r) < 0f)
                        r = new Quaternion(-r.x, -r.y, -r.z, -r.w);
                }

                qAcc += new Vector4(r.x, r.y, r.z, r.w) * w;
            }

            if (!hasAny || Mathf.Abs(weightSum) <= 1e-8f)
            {
                outputMatrixMaya = Matrix4x4.identity;
                PublishMatrix(options, outputMatrixMaya);
                SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, terms={termCount}, weightSum≈0 => identity");
                return;
            }

            Vector3 tAvg = tSum / weightSum;
            Vector3 sAvg = sSum / weightSum;

            Quaternion rAvg = Quaternion.identity;
            float mag = Mathf.Sqrt(qAcc.x * qAcc.x + qAcc.y * qAcc.y + qAcc.z * qAcc.z + qAcc.w * qAcc.w);
            if (mag > 1e-8f)
                rAvg = new Quaternion(qAcc.x / mag, qAcc.y / mag, qAcc.z / mag, qAcc.w / mag);

            outputMatrixMaya = MatrixUtil.ComposeTRS(tAvg, rAvg, sAvg);

            PublishMatrix(options, outputMatrixMaya);

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, terms={termCount}, weightSum={weightSum:0.###} => MayaMatrixValue published");
        }

        private void PublishMatrix(MayaImportOptions options, Matrix4x4 mayaM)
        {
            var mv = GetComponent<MayaMatrixValue>() ?? gameObject.AddComponent<MayaMatrixValue>();
            mv.valid = true;
            mv.matrixMaya = mayaM;
            mv.matrixUnity = MayaToUnityConversion.ConvertMatrix(mayaM, options.Conversion);
        }

        private List<int> CollectWtMatrixIndices()
        {
            var set = new HashSet<int>();

            // attrs
            if (Attributes != null)
            {
                for (int i = 0; i < Attributes.Count; i++)
                {
                    var a = Attributes[i];
                    if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                    if (TryExtractWtMatrixIndex(a.Key, out int idx))
                        set.Add(idx);
                }
            }

            // conns (dst)
            if (Connections != null)
            {
                for (int i = 0; i < Connections.Count; i++)
                {
                    var c = Connections[i];
                    if (c == null) continue;
                    if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                        continue;

                    var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                    if (string.IsNullOrEmpty(dstAttr)) continue;

                    if (TryExtractWtMatrixIndex(dstAttr, out int idx))
                        set.Add(idx);
                }
            }

            return new List<int>(set);
        }

        private static bool TryExtractWtMatrixIndex(string s, out int idx)
        {
            idx = -1;
            if (string.IsNullOrEmpty(s)) return false;

            int p = s.IndexOf("wtMatrix[", StringComparison.Ordinal);
            if (p < 0) p = s.IndexOf(".wtMatrix[", StringComparison.Ordinal);
            if (p < 0) return false;

            int lb = s.IndexOf('[', p);
            int rb = s.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            string inner = s.Substring(lb + 1, rb - lb - 1);
            int colon = inner.IndexOf(':');
            if (colon >= 0) inner = inner.Substring(0, colon);

            return int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        }

        private string FindIncomingToExact(params string[] dstAttrs)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (dstAttrs == null || dstAttrs.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int k = 0; k < dstAttrs.Length; k++)
                {
                    if (string.Equals(dstAttr, dstAttrs[k], StringComparison.Ordinal))
                        return NormalizePlug(c.SrcPlug);
                }
            }

            return null;
        }

        private static string NormalizePlug(string plug)
        {
            if (string.IsNullOrEmpty(plug)) return null;
            plug = plug.Trim();
            if (plug.Length >= 2 && plug[0] == '"' && plug[plug.Length - 1] == '"')
                plug = plug.Substring(1, plug.Length - 2);
            return plug;
        }

        private static string NodePart(string plug) => MayaPlugUtil.ExtractNodePart(plug);

        private Matrix4x4 ResolveConnectedMatrix(string srcPlug, out bool got)
        {
            got = false;
            if (string.IsNullOrEmpty(srcPlug)) return Matrix4x4.identity;

            var n = NodePart(srcPlug);
            if (string.IsNullOrEmpty(n)) return Matrix4x4.identity;

            var tr = MayaNodeLookup.FindTransform(n);
            if (tr == null) return Matrix4x4.identity;

            var mv = tr.GetComponent<MayaMatrixValue>();
            if (mv != null && mv.valid)
            {
                got = true;
                return mv.matrixMaya;
            }

            return Matrix4x4.identity;
        }

        private float ResolveConnectedFloat(string srcPlug, float fallback)
        {
            if (string.IsNullOrEmpty(srcPlug)) return fallback;

            var n = NodePart(srcPlug);
            if (string.IsNullOrEmpty(n)) return fallback;

            var tr = MayaNodeLookup.FindTransform(n);
            if (tr == null) return fallback;

            // Core float carrier
            var fvCore = tr.GetComponent<MayaImporter.Core.MayaFloatValue>();
            if (fvCore != null && fvCore.valid) return fvCore.value;

            // Nodes float carrier（存在しても OK）
            var fvNodes = tr.GetComponent<MayaImporter.Nodes.MayaFloatValue>();
            if (fvNodes != null && fvNodes.valid) return fvNodes.value;

            return fallback;
        }
    }
}
