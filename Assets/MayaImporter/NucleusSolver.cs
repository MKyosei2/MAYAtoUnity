using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    [DisallowMultipleComponent]
    public abstract class NucleusSolverBase : MayaNodeComponentBase
    {
        [Header("Resolved (best-effort)")]
        public Vector3 gravityDirection = Vector3.down;
        public float gravityMagnitude = 9.8f;
        public float timeScale = 1f;
        public int subSteps = 4;
        public float spaceScale = 1f;

        [Header("Connected Nodes (names)")]
        public List<string> connectedDynamicsNodes = new List<string>();

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            // gravity magnitude
            gravityMagnitude = ReadFloat(".gravity", ".grav", gravityMagnitude);

            // gravity direction (double3)
            if (TryReadVector3(".gravityDirection", ".gdir", out var gd))
            {
                if (gd.sqrMagnitude > 1e-10f)
                    gravityDirection = gd.normalized;
            }

            timeScale = ReadFloat(".timeScale", ".ts", timeScale);
            spaceScale = ReadFloat(".spaceScale", ".ssc", spaceScale);

            subSteps = ReadInt(".subSteps", ".ss", subSteps);
            if (subSteps < 1) subSteps = 1;
            if (subSteps > 64) subSteps = 64;

            // Runtime world holder (Unity-only)
            var world = GetComponent<MayaNucleusRuntimeWorld>();
            if (world == null) world = gameObject.AddComponent<MayaNucleusRuntimeWorld>();

            world.SourceNodeName = NodeName;
            world.GravityDirection = gravityDirection;
            world.GravityMagnitude = gravityMagnitude;
            world.TimeScale = timeScale;
            world.SubSteps = subSteps;
            world.SpaceScale = spaceScale;

            // Collect connected dynamics nodes (nCloth / nParticle Œn)
            connectedDynamicsNodes.Clear();

            var scene = MayaBuildContext.CurrentScene;
            if (scene != null)
            {
                CollectConnectedDynamicsNodes(scene, NodeName, connectedDynamicsNodes);
                world.ConnectedDynamicsNodes = new List<string>(connectedDynamicsNodes);
            }

            log.Info($"[nucleus] '{NodeName}' g={gravityMagnitude} dir={gravityDirection} ts={timeScale} ss={subSteps} ssc={spaceScale} connected={connectedDynamicsNodes.Count}");
        }

        // ------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------

        private float ReadFloat(string k1, string k2, float def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f1)) return f1;
            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0 && TryF(a.Tokens[0], out var f2)) return f2;
            return def;
        }

        private int ReadInt(string k1, string k2, int def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0 && int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v1)) return v1;
            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0 && int.TryParse(a.Tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v2)) return v2;
            return def;
        }

        private bool TryReadVector3(string k1, string k2, out Vector3 v)
        {
            v = Vector3.zero;

            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var x1) && TryF(a.Tokens[1], out var y1) && TryF(a.Tokens[2], out var z1))
            {
                v = new Vector3(x1, y1, z1);
                return true;
            }

            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var x2) && TryF(a.Tokens[1], out var y2) && TryF(a.Tokens[2], out var z2))
            {
                v = new Vector3(x2, y2, z2);
                return true;
            }

            return false;
        }

        private static bool TryF(string s, out float f)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }

        private static void CollectConnectedDynamicsNodes(MayaSceneData scene, string nucleusName, List<string> outList)
        {
            if (scene?.Connections == null) return;

            for (int i = 0; i < scene.Connections.Count; i++)
            {
                var c = scene.Connections[i];
                if (c == null) continue;

                var srcNode = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                var dstNode = MayaPlugUtil.ExtractNodePart(c.DstPlug);

                bool isSrc = MayaPlugUtil.NodeMatches(srcNode, nucleusName);
                bool isDst = MayaPlugUtil.NodeMatches(dstNode, nucleusName);
                if (!isSrc && !isDst) continue;

                var other = isSrc ? dstNode : srcNode;
                if (string.IsNullOrEmpty(other)) continue;

                if (!LooksDynamicsNode(scene, other)) continue;

                // unique by leaf match
                var leaf = MayaPlugUtil.LeafName(other);
                bool exists = false;
                for (int k = 0; k < outList.Count; k++)
                {
                    if (string.Equals(MayaPlugUtil.LeafName(outList[k]), leaf, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) outList.Add(other);
            }
        }

        private static bool LooksDynamicsNode(MayaSceneData scene, string nodeName)
        {
            var rec = FindNodeByAnyName(scene, nodeName);
            if (rec == null) return false;

            var t = rec.NodeType ?? "";
            if (string.IsNullOrEmpty(t)) return false;

            // nucleus / nCloth / nParticle Œn‚ðL‚ß‚ÉE‚¤
            return t.IndexOf("nCloth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   t.IndexOf("nParticle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   t.IndexOf("hairSystem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   t.IndexOf("fluid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   t.IndexOf("nucleus", StringComparison.OrdinalIgnoreCase) >= 0;
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
