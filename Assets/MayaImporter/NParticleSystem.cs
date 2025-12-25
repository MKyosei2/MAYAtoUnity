using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [MayaNodeType("nParticleSystem")]
    [DisallowMultipleComponent]
    public sealed class NParticleSystem : MayaNodeComponentBase
    {
        [Header("Resolved (best-effort)")]
        public float emissionRate = 50f;
        public float startLifetime = 2f;
        public float startSpeed = 2f;
        public float startSize = 0.1f;
        public int maxParticles = 2000;

        public Color startColor = Color.white;

        [Header("Connections (names)")]
        public string connectedNucleusNode;
        public List<string> connectedFieldNodes = new List<string>();

        [Header("Unity")]
        public ParticleSystem particleSystem;
        public MayaNParticleRuntimeSystem runtime;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            // Read common-ish attrs (broad keys)
            emissionRate = ReadFloat(".rate", ".emissionRate", emissionRate);
            startLifetime = ReadFloat(".lifespan", ".lifeSpan", startLifetime);
            startSpeed = ReadFloat(".speed", ".startSpeed", startSpeed);
            startSize = ReadFloat(".particleSize", ".radius", startSize);
            maxParticles = ReadInt(".maxCount", ".maxParticles", maxParticles);
            if (maxParticles < 64) maxParticles = 64;

            if (TryReadColor(".color", ".startColor", out var c))
                startColor = c;

            // Create/Configure Unity ParticleSystem
            particleSystem = GetComponent<ParticleSystem>();
            if (particleSystem == null) particleSystem = gameObject.AddComponent<ParticleSystem>();

            ConfigureParticleSystem(particleSystem);

            // Resolve nucleus + fields from scene connections
            var scene = MayaBuildContext.CurrentScene;
            connectedNucleusNode = FindConnectedNucleus(scene, NodeName);
            connectedFieldNodes = FindConnectedFields(scene, NodeName);

            var nucTf = MayaNodeLookup.FindTransform(connectedNucleusNode);
            var nucWorld = nucTf != null ? nucTf.GetComponent<MayaNucleusRuntimeWorld>() : null;

            runtime = GetComponent<MayaNParticleRuntimeSystem>();
            if (runtime == null) runtime = gameObject.AddComponent<MayaNParticleRuntimeSystem>();

            runtime.SourceNodeName = NodeName;
            runtime.ParticleSystem = particleSystem;
            runtime.NucleusWorld = nucWorld;
            runtime.FieldNodeNames = new List<string>(connectedFieldNodes);

            MayaParticleManager.EnsureExists();

            log.Info($"[nParticleSystem] '{NodeName}' rate={emissionRate} life={startLifetime} spd={startSpeed} size={startSize} max={maxParticles} nucleus='{MayaPlugUtil.LeafName(connectedNucleusNode)}' fields={connectedFieldNodes.Count}");
        }

        private void ConfigureParticleSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.maxParticles = maxParticles;
            main.startLifetime = startLifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.startColor = startColor;

            // We apply nucleus gravity ourselves (directional), so keep builtin gravity at 0.
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;

            // Basic shape enabled (best-effort)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
        }

        private float ReadFloat(string k1, string k2, float def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f1)) return f1;
            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f2)) return f2;
            return def;
        }

        private int ReadInt(string k1, string k2, int def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v1))
                return v1;

            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0 &&
                int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v2))
                return v2;

            return def;
        }

        private bool TryReadColor(string k1, string k2, out Color c)
        {
            c = Color.white;

            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var r1) && TryF(a.Tokens[1], out var g1) && TryF(a.Tokens[2], out var b1))
            {
                float a1 = 1f;
                if (a.Tokens.Count >= 4 && TryF(a.Tokens[3], out var aa)) a1 = aa;
                c = new Color(r1, g1, b1, a1);
                return true;
            }

            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var r2) && TryF(a.Tokens[1], out var g2) && TryF(a.Tokens[2], out var b2))
            {
                float a2 = 1f;
                if (a.Tokens.Count >= 4 && TryF(a.Tokens[3], out var aa)) a2 = aa;
                c = new Color(r2, g2, b2, a2);
                return true;
            }

            return false;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);

        private static string FindConnectedNucleus(MayaSceneData scene, string self)
        {
            if (scene?.Connections == null) return null;

            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;

                var a = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                var b = MayaPlugUtil.ExtractNodePart(c.DstPlug);

                bool hitA = MayaPlugUtil.NodeMatches(a, self);
                bool hitB = MayaPlugUtil.NodeMatches(b, self);
                if (!hitA && !hitB) continue;

                var other = hitA ? b : a;
                if (string.IsNullOrEmpty(other)) continue;

                var rec = FindNodeByAnyName(scene, other);
                if (rec == null) continue;

                var t = rec.NodeType ?? "";
                if (t.IndexOf("nucleus", StringComparison.OrdinalIgnoreCase) >= 0)
                    return other;
            }

            return null;
        }

        private static List<string> FindConnectedFields(MayaSceneData scene, string self)
        {
            var list = new List<string>();
            if (scene?.Connections == null) return list;

            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;

                var a = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                var b = MayaPlugUtil.ExtractNodePart(c.DstPlug);

                bool hitA = MayaPlugUtil.NodeMatches(a, self);
                bool hitB = MayaPlugUtil.NodeMatches(b, self);
                if (!hitA && !hitB) continue;

                var other = hitA ? b : a;
                if (string.IsNullOrEmpty(other)) continue;

                var rec = FindNodeByAnyName(scene, other);
                if (rec == null) continue;

                var t = rec.NodeType ?? "";
                if (t.EndsWith("Field", StringComparison.OrdinalIgnoreCase) ||
                    t.IndexOf("Field", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var leaf = MayaPlugUtil.LeafName(other);
                    bool exists = false;
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (string.Equals(MayaPlugUtil.LeafName(list[k]), leaf, StringComparison.Ordinal))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) list.Add(other);
                }
            }

            return list;
        }

        private static NodeRecord FindNodeByAnyName(MayaSceneData scene, string nameOrDag)
        {
            if (scene?.Nodes == null || string.IsNullOrEmpty(nameOrDag)) return null;

            if (scene.Nodes.TryGetValue(nameOrDag, out var exact) && exact != null)
                return exact;

            var leaf = MayaPlugUtil.LeafName(nameOrDag);
            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;
                if (string.Equals(MayaPlugUtil.LeafName(n.Name), leaf, StringComparison.Ordinal))
                    return n;
            }
            return null;
        }
    }
}
