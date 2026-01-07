// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;

namespace MayaImporter.Deformers
{
    [MayaNodeType("blendShape")]
    public sealed class BlendShapeNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            var meta = GetComponent<MayaBlendShapeMetadata>()
                       ?? gameObject.AddComponent<MayaBlendShapeMetadata>();

            meta.targets.Clear();

            // ✅ 1) Weight配列を「連番前提」で読まない（穴があると壊れる）
            // Attributesを一回走査して、.w[i] / .weight[i] / range を全部拾う
            var weights = CollectAllWeightIndices();

            // ✅ 2) Attributes から inputTargetGroup / itg の index を拾って「存在するターゲット」を補完する
            // weight が無い(=0扱い)でも、埋め込みデルタがあるケースを拾えるようにする
            var groups = CollectAllTargetGroupIndicesFromAttributes();
            foreach (var gi in groups)
            {
                if (!weights.ContainsKey(gi))
                    weights[gi] = 0f;
            }

            // targets を index 昇順で確定
            foreach (var kv in weights)
            {
                int idx = kv.Key;
                float w = kv.Value;

                string targetMesh = ResolveTargetMeshBestEffort(idx);

                // ✅ Connections で取れない場合でも「embedded itg」を目印として残す
                if (string.IsNullOrEmpty(targetMesh))
                {
                    if (groups.Contains(idx))
                        targetMesh = $"embedded_itg[{idx}]";
                    else
                        targetMesh = $"target_{idx}";
                }

                var t = new MayaBlendShapeMetadata.Target
                {
                    targetIndex = idx,
                    targetMesh = targetMesh,
                    name = null, // ✅ Reconstruction側で確定名を保存する
                    weight = w,
                };
                meta.targets.Add(t);
            }

            log?.Info($"[blendShape] targets(meta)={meta.targets.Count} (weights+groups)");

            // ✅ 3) Reconstruct Unity blendshapes（Connectionsが無くても埋め込みiptからbest-effort decode）
            MayaBlendShapeReconstruction.TryBuildBlendShapes(this, meta, log);
        }

        // ------------------------------------------------------------
        // Weight collection (supports holes + ranges)
        // ------------------------------------------------------------

        private SortedDictionary<int, float> CollectAllWeightIndices()
        {
            var map = new SortedDictionary<int, float>();

            if (Attributes == null || Attributes.Count == 0)
                return map;

            for (int ai = 0; ai < Attributes.Count; ai++)
            {
                var a = Attributes[ai];
                if (a == null) continue;

                var key = a.Key ?? "";
                if (!LooksLikeWeightKey(key, out int start, out int end, out bool isRange))
                    continue;

                if (a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                if (!isRange)
                {
                    // single index
                    if (TryFloatPreferLast(a.Tokens, out var w))
                        map[start] = w; // last wins
                    continue;
                }

                // range: weight[0:3] tokens may be 4 values
                int count = (end - start + 1);

                // NOTE: Tokens に数値以外が混じる場合があるので、floatだけ拾う
                var floats = CollectFloats(a.Tokens);

                int n = Mathf.Min(count, floats.Count);
                for (int i = 0; i < n; i++)
                    map[start + i] = floats[i];
            }

            return map;
        }

        private static bool LooksLikeWeightKey(string key, out int start, out int end, out bool isRange)
        {
            start = end = -1;
            isRange = false;

            if (string.IsNullOrEmpty(key)) return false;

            // Accept:
            // ".w[3]" ".weight[3]" "w[3]" "weight[3]"
            // ".w[0:7]" etc.
            string k = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : key;

            bool okPrefix =
                k.StartsWith("w[", StringComparison.Ordinal) ||
                k.StartsWith("weight[", StringComparison.Ordinal);

            if (!okPrefix) return false;

            int lb = k.IndexOf('[');
            int rb = k.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            var inner = k.Substring(lb + 1, rb - lb - 1); // "3" or "0:7"
            int colon = inner.IndexOf(':');

            if (colon < 0)
            {
                if (!int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    return false;
                start = end = idx;
                isRange = false;
                return true;
            }

            var a = inner.Substring(0, colon);
            var b = inner.Substring(colon + 1);

            if (!int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start)) return false;
            if (!int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out end)) return false;

            if (end < start) (start, end) = (end, start);
            isRange = true;
            return true;
        }

        private static bool TryFloatPreferLast(List<string> tokens, out float f)
        {
            f = 0f;
            if (tokens == null || tokens.Count == 0) return false;

            for (int i = tokens.Count - 1; i >= 0; i--)
                if (TryFloat(tokens[i], out f)) return true;

            return false;
        }

        private static bool TryFloat(string s, out float f)
            => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private static List<float> CollectFloats(List<string> tokens)
        {
            var list = new List<float>(tokens != null ? tokens.Count : 0);
            if (tokens == null) return list;

            for (int i = 0; i < tokens.Count; i++)
                if (TryFloat(tokens[i], out var f))
                    list.Add(f);

            return list;
        }

        // ------------------------------------------------------------
        // Target group discovery from Attributes (itg/inputTargetGroup)
        // ------------------------------------------------------------

        private HashSet<int> CollectAllTargetGroupIndicesFromAttributes()
        {
            var set = new HashSet<int>();
            if (Attributes == null || Attributes.Count == 0) return set;

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                // patterns:
                // ".itg[3].iti[6000].ipt" / "itg[3]..."
                // ".inputTargetGroup[3].inputTargetItem[6000]..."
                if (TryExtractIndexAfterToken(a.Key, "itg[", out int idx) ||
                    TryExtractIndexAfterToken(a.Key, "inputTargetGroup[", out idx))
                {
                    if (idx >= 0) set.Add(idx);
                }
            }

            return set;
        }

        private static bool TryExtractIndexAfterToken(string s, string token, out int idx)
        {
            idx = -1;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(token)) return false;

            int p = s.IndexOf(token, StringComparison.Ordinal);
            if (p < 0) return false;

            int lb = p + token.Length;
            int rb = s.IndexOf(']', lb);
            if (rb < 0 || rb <= lb) return false;

            // allow "0:7" -> take start
            string inside = s.Substring(lb, rb - lb);
            int colon = inside.IndexOf(':');
            if (colon >= 0) inside = inside.Substring(0, colon);

            return int.TryParse(inside, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx);
        }

        // ------------------------------------------------------------
        // Target mesh resolution (best effort from connections + aliases)
        // ------------------------------------------------------------

        private string ResolveTargetMeshBestEffort(int index)
        {
            if (Connections == null) return null;

            // Maya の Plug 表記は長短が混在する
            // long: ".inputTargetGroup[i]" ".inputTargetItem[...]"
            // short: ".itg[i]" ".iti[...]"
            var needleLong = $".inputTargetGroup[{index}]";
            var needleShort = $".itg[{index}]";

            // Prefer incoming target connections mentioning the target group index
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                // まず index で絞る
                bool hit = c.DstPlug.Contains(needleLong, StringComparison.Ordinal) ||
                           c.DstPlug.Contains(needleShort, StringComparison.Ordinal);

                if (!hit) continue;

                // 次に「geometry系の差し込み先」を優先
                // 例: inputGeomTarget / inputPointsTarget / inputComponentsTarget 等
                var d = c.DstPlug;
                bool looksGeom =
                    d.IndexOf("inputGeomTarget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.IndexOf("inputPointsTarget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.IndexOf("inputComponentsTarget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.IndexOf(".ipt", StringComparison.Ordinal) >= 0 ||
                    d.IndexOf(".ict", StringComparison.Ordinal) >= 0;

                if (!looksGeom)
                {
                    // index hit はしているので、候補としては残す（ただし後回し）
                    // 続行せずに "候補" として記録
                }

                if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
            }

            // Fallback: any incoming target connection mentioning inputTarget/itg
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                var dst = c.DstPlug;

                if (dst.IndexOf(".inputTarget", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dst.IndexOf(".itg[", StringComparison.Ordinal) >= 0)
                {
                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }
    }
}


// ----------------------------------------------------------------------------- 
// INTEGRATED: BlendShapeEvalNode.cs
// -----------------------------------------------------------------------------
// PATCH: ProductionImpl v6 (Unity-only, retention-first)

namespace MayaImporter.Phase3.Evaluation
{
    /// <summary>
    /// BlendShape Deformer EvalNode
    /// ���ό`�� Unity ���s�����߁A�]���� Dirty �`�d�̂�
    /// </summary>
    public class BlendShapeEvalNode : EvalNode
    {
        private readonly MayaNode _mayaNode;

        public BlendShapeEvalNode(MayaNode node)
            : base(node.NodeName)
        {
            _mayaNode = node;
        }

        protected override void Evaluate(EvalContext ctx)
        {
            if (ctx == null)
                return;

            // Maya �I�ɂ� outMesh ���X�V�����
            ctx.MarkAttributeDirty($"{NodeName}.outMesh");
        }
    }
}
