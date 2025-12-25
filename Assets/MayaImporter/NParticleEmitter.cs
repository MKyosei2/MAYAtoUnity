using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nParticleEmitter")]
    [DisallowMultipleComponent]
    public sealed class NParticleEmitter : MayaNodeComponentBase
    {
        [Header("Resolved (best-effort)")]
        public float rate = 50f;
        public float speed = 1f;
        public Vector3 direction = Vector3.up;

        [Header("Unity")]
        public string targetParticleSystemNode;
        public Transform targetParticleSystemTransform;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            rate = ReadFloat(".rate", ".emissionRate", rate);
            speed = ReadFloat(".speed", ".spd", speed);

            if (TryReadVec3(".direction", ".dir", out var d) && d.sqrMagnitude > 1e-10f)
                direction = d;

            // best-effort: find connected nParticleSystem node
            targetParticleSystemNode = ResolveConnectedParticleSystemNode();
            targetParticleSystemTransform = MayaNodeLookup.FindTransform(targetParticleSystemNode);

            if (targetParticleSystemTransform != null)
            {
                var ps = targetParticleSystemTransform.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var emission = ps.emission;
                    if (emission.enabled)
                        emission.rateOverTime = rate;

                    var main = ps.main;
                    main.startSpeed = Mathf.Max(0f, speed);

                    var shape = ps.shape;
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                }
            }

            log.Info($"[nParticleEmitter] '{NodeName}' rate={rate} speed={speed} dir={direction} target='{MayaPlugUtil.LeafName(targetParticleSystemNode)}'");
        }

        private string ResolveConnectedParticleSystemNode()
        {
            // Outgoing connections to nParticleSystem or nParticle
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Source && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dst = c.DstNodePart ?? MayaPlugUtil.ExtractNodePart(c.DstPlug);
                if (!string.IsNullOrEmpty(dst))
                    return dst;
            }

            // Fallback: any incoming
            for (int i = 0; i < Connections.Count; i++)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination && c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var src = c.SrcNodePart ?? MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                if (!string.IsNullOrEmpty(src))
                    return src;
            }

            return null;
        }

        private float ReadFloat(string k1, string k2, float def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f1))
                return f1;

            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0 &&
                float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f2))
                return f2;

            return def;
        }

        private bool TryReadVec3(string k1, string k2, out Vector3 v)
        {
            v = Vector3.zero;

            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x1) &&
                float.TryParse(a.Tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y1) &&
                float.TryParse(a.Tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z1))
            {
                v = new Vector3(x1, y1, z1);
                return true;
            }

            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x2) &&
                float.TryParse(a.Tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y2) &&
                float.TryParse(a.Tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z2))
            {
                v = new Vector3(x2, y2, z2);
                return true;
            }

            return false;
        }
    }
}
