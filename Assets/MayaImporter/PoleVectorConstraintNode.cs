using System.Globalization;
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Components;
using MayaImporter.IK;

namespace MayaImporter.Animation
{
    [MayaNodeType("poleVectorConstraint")]
    public sealed class PoleVectorConstraintNode : MayaNodeComponentBase
    {
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            var meta = GetOrAdd<MayaConstraintMetadata>();
            meta.constraintType = "poleVector";
            meta.maintainOffset = false;
            meta.targets.Clear();

            // poleVectorConstraint: usually single target
            // try read weight
            float w = ReadFloat(".w0", 1f);
            if (TryGetAttr(".tg[0].tw", out var tw) && tw.Tokens != null && tw.Tokens.Count > 0)
                w = ReadFloatFromAttr(tw, w);

            var targetName =
                ResolveIncomingSourceNode(".tg[0].targetTranslate") ??
                ResolveIncomingSourceNode(".tg[0].targetParentMatrix") ??
                ResolveIncomingSourceNode(".tg[0].targetParent") ??
                "target_0";

            meta.targets.Add(new MayaConstraintMetadata.Target
            {
                targetNodeName = targetName,
                weight = w
            });

            // constrained: typically ikHandle
            var constrainedName = ResolveConstrainedNodeName();
            var constrainedTf = MayaNodeLookup.FindTransform(constrainedName);

            if (constrainedTf == null)
            {
                log?.Warn($"[poleVectorConstraint] constrained not found: '{constrainedName}'");
                return;
            }

            var ik = constrainedTf.GetComponent<MayaIkHandleComponent>();
            if (ik == null)
            {
                // ikHandle node GO may not be the constrainedTf itself; best-effort:
                var ikNode = constrainedTf.GetComponent<MayaIkHandleNodeComponent>();
                if (ikNode != null)
                    ik = ikNode.GetComponent<MayaIkHandleComponent>();

                ik ??= constrainedTf.gameObject.AddComponent<MayaIkHandleComponent>();
            }

            var targetTf = MayaNodeLookup.FindTransform(targetName);
            ik.PoleVector = targetTf;

            // FIX: avoid nested escaping bugs by composing string plainly
            string poleName = targetTf != null ? targetTf.name : "(null)";
            log?.Info($"[poleVectorConstraint] ikHandle='{constrainedTf.name}' pole='{poleName}' weight={w}");
        }

        private string ResolveConstrainedNodeName()
        {
            if (Connections == null || Connections.Count == 0) return null;

            // prefer destination plugs that look like ikHandle poleVector / pv / twist etc
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug) ?? "";
                if (dstAttr.Contains("poleVector", System.StringComparison.Ordinal) ||
                    dstAttr.Contains(".pv", System.StringComparison.Ordinal) ||
                    dstAttr.Contains("pv", System.StringComparison.Ordinal))
                    return c.DstNodePart;
            }

            // fallback: first outgoing destination node
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                return c.DstNodePart;
            }

            return null;
        }

        private string ResolveIncomingSourceNode(string dstPlugSuffix)
        {
            if (Connections == null || Connections.Count == 0) return null;
            var suf = NormalizeSuffix(dstPlugSuffix);

            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                if (string.IsNullOrEmpty(c.DstPlug)) continue;

                if (EndsWithSuffixCompat(c.DstPlug, suf))
                {
                    if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
                    return MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                }
            }

            return null;
        }

        private float ReadFloat(string key, float def)
        {
            if (!TryGetAttr(key, out var a) || a.Tokens == null || a.Tokens.Count == 0) return def;
            return float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;
        }

        private float ReadFloatFromAttr(SerializedAttribute a, float def)
        {
            if (a == null || a.Tokens == null || a.Tokens.Count == 0) return def;
            return float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : def;
        }

        private static string NormalizeSuffix(string s)
            => string.IsNullOrEmpty(s) ? s : (s.StartsWith(".", System.StringComparison.Ordinal) ? s : "." + s);

        private static bool EndsWithSuffixCompat(string plug, string suffixWithDot)
        {
            if (plug.EndsWith(suffixWithDot, System.StringComparison.Ordinal)) return true;
            var noDot = suffixWithDot.StartsWith(".", System.StringComparison.Ordinal) ? suffixWithDot.Substring(1) : suffixWithDot;
            return plug.EndsWith(noDot, System.StringComparison.Ordinal);
        }

        private T GetOrAdd<T>() where T : Component
            => GetComponent<T>() ?? gameObject.AddComponent<T>();
    }
}
