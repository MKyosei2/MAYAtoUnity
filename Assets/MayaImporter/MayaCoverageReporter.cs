// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MayaImporter.Core
{
    public static class MayaCoverageReporter
    {
        private static readonly HashSet<string> KnownCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "fileInfo","requires","currentUnit","createNode","setAttr","connectAttr","disconnectAttr",
            "parent","select","rename","addAttr","deleteAttr","lockNode","setKeyframe","setDrivenKeyframe",
            "connectDynamic","animLayer","doCreateGeometryCache","doImportCacheFile","playbackOptions",
            "workspace","scriptNode","evalDeferred","namespace","shadingNode"
        };

        private static readonly HashSet<string> DagLikeNodeTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "transform","joint","locator",
            "mesh","nurbsCurve","nurbsSurface","subdiv","camera",
            "ikHandle","ikEffector","clusterHandle",
        };

        public static CoverageResult Generate(MayaSceneData scene)
        {
            var counts = scene.CountNodeTypes();

            var rows = new List<NodeTypeRow>();
            foreach (var kv in counts.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal))
            {
                var nodeType = kv.Key;
                var count = kv.Value;

                bool createsGO = IsDagLike(nodeType);
                bool createsComponent = IsUnityComponentLike(nodeType);

                rows.Add(new NodeTypeRow
                {
                    NodeType = nodeType,
                    Count = count,
                    RawCaptured = true,
                    CreatesGameObject = createsGO,
                    CreatesUnityComponent = createsComponent
                });
            }

            // Unknown command counts (audit)
            var unknownCmdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (scene.RawStatements != null && scene.RawStatements.Count > 0)
            {
                foreach (var st in scene.RawStatements)
                {
                    var cmd = st.Command ?? "";
                    if (string.IsNullOrEmpty(cmd)) continue;
                    if (KnownCommands.Contains(cmd)) continue;

                    unknownCmdCounts.TryGetValue(cmd, out var c);
                    unknownCmdCounts[cmd] = c + 1;
                }
            }

            // Limitations reports
            var meshLimitations = MayaMeshLimitationsReporter.Collect(scene);
            var skinLimitations = MayaSkinClusterLimitationsReporter.Collect(scene);
            var blendLimitations = MayaBlendShapeLimitationsReporter.Collect(scene);
            var rigLimitations = MayaRigLimitationsReporter.Collect(scene);
            var animLimitations = MayaAnimationEvaluationLimitationsReporter.Collect(scene);
            var shadingLimitations = MayaShadingNetworkLimitationsReporter.Collect(scene);

            // NEW: dynamics / FX limitations (UnityɊTO)
            var dynamicsFxLimitations = MayaDynamicsFxLimitationsReporter.Collect(scene);

            return new CoverageResult
            {
                NodeTypeRows = rows,
                UnknownCommandCounts = unknownCmdCounts,

                MeshLimitations = meshLimitations,
                SkinClusterLimitations = skinLimitations,
                BlendShapeLimitations = blendLimitations,
                RigLimitations = rigLimitations,
                AnimationLimitations = animLimitations,
                ShadingLimitations = shadingLimitations,

                DynamicsFxLimitations = dynamicsFxLimitations
            };
        }

        public static void WriteCsvReports(string sourcePath, CoverageResult result, HashSet<string> maya2026StandardNodeTypesOrNull, MayaImportLog log)
        {
            var baseName = Path.GetFileNameWithoutExtension(sourcePath);
            var dir = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(dir)) dir = ".";

            // 1) nodeType counts
            var countsPath = Path.Combine(dir, baseName + "__nodeTypeCounts.csv");
            File.WriteAllText(countsPath, ToNodeTypeCountsCsv(result, maya2026StandardNodeTypesOrNull), Encoding.UTF8);
            log?.Info("Wrote CSV: " + countsPath);

            // 2) unknown commands
            var unknownPath = Path.Combine(dir, baseName + "__unknownCommands.csv");
            File.WriteAllText(unknownPath, ToUnknownCommandsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + unknownPath);

            // 3) missing standard nodeTypes (optional)
            if (maya2026StandardNodeTypesOrNull != null && maya2026StandardNodeTypesOrNull.Count > 0)
            {
                var present = new HashSet<string>(result.NodeTypeRows.Select(r => r.NodeType), StringComparer.Ordinal);
                var missing = maya2026StandardNodeTypesOrNull.Where(t => !present.Contains(t))
                    .OrderBy(t => t, StringComparer.Ordinal)
                    .ToList();

                var missingPath = Path.Combine(dir, baseName + "__missingStandardNodeTypes.csv");
                File.WriteAllText(missingPath, ToMissingStandardCsv(missing), Encoding.UTF8);
                log?.Info("Wrote CSV: " + missingPath);
            }
            else
            {
                log?.Warn("Standard node type list not found. (Optional) Put Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt to enable missing-standard report.");
            }

            // 4) mesh limitations
            var meshPath = Path.Combine(dir, baseName + "__meshLimitations.csv");
            File.WriteAllText(meshPath, ToMeshLimitationsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + meshPath);

            // 5) skinCluster limitations
            var skinPath = Path.Combine(dir, baseName + "__skinClusterLimitations.csv");
            File.WriteAllText(skinPath, ToSkinLimitationsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + skinPath);

            // 6) blendShape limitations
            var blendPath = Path.Combine(dir, baseName + "__blendShapeLimitations.csv");
            File.WriteAllText(blendPath, ToBlendLimitationsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + blendPath);

            // 7) rig limitations
            var rigPath = Path.Combine(dir, baseName + "__rigLimitations.csv");
            File.WriteAllText(rigPath, ToRigLimitationsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + rigPath);

            // 8) animation evaluation limitations
            var animPath = Path.Combine(dir, baseName + "__animationLimitations.csv");
            File.WriteAllText(animPath, ToAnimationLimitationsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + animPath);

            // 9) shading network limitations
            var shadingPath = Path.Combine(dir, baseName + "__shadingLimitations.csv");
            File.WriteAllText(shadingPath, ToShadingLimitationsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + shadingPath);

            // 10) dynamics / FX limitations (NEW)
            var dynPath = Path.Combine(dir, baseName + "__dynamicsFxLimitations.csv");
            File.WriteAllText(dynPath, ToDynamicsFxLimitationsCsv(result), Encoding.UTF8);
            log?.Info("Wrote CSV: " + dynPath);
        }

        private static bool IsDagLike(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;

            if (DagLikeNodeTypes.Contains(nodeType)) return true;
            if (nodeType.EndsWith("Light", StringComparison.Ordinal)) return true;
            if (nodeType.EndsWith("Shape", StringComparison.Ordinal)) return true;
            if (nodeType.EndsWith("Handle", StringComparison.Ordinal)) return true;

            return false;
        }

        private static bool IsUnityComponentLike(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;

            if (nodeType == "camera") return true;
            if (nodeType.EndsWith("Light", StringComparison.Ordinal)) return true;
            if (nodeType == "mesh") return true;

            if (nodeType == "blendShape") return true;
            if (nodeType == "skinCluster") return true;

            if (nodeType.EndsWith("Constraint", StringComparison.Ordinal)) return true;
            if (nodeType == "ikHandle") return true;
            if (nodeType == "motionPath") return true;

            if (nodeType == "animCurveTL" || nodeType == "animCurveTA" || nodeType == "animCurveTU" ||
                nodeType == "animCurveTT" || nodeType == "animCurveUL" || nodeType == "animCurveUA" ||
                nodeType == "animCurveUT" || nodeType == "animCurveUU")
                return true;

            if (nodeType == "animLayer") return true;

            if (nodeType == "shadingEngine") return true;
            if (nodeType == "file") return true;
            if (nodeType.EndsWith("Shader", StringComparison.Ordinal)) return true;

            // dynamics/FX: component-like output in Unity would require custom components/solvers
            if (nodeType == "nucleus") return true;
            if (nodeType == "nCloth") return true;
            if (nodeType == "nHair") return true;
            if (nodeType == "hairSystem") return true;
            if (nodeType == "nParticle") return true;
            if (nodeType == "particle") return true;
            if (nodeType.StartsWith("field", StringComparison.Ordinal)) return true;

            return false;
        }

        private static string ToNodeTypeCountsCsv(CoverageResult result, HashSet<string> standardOrNull)
        {
            var sb = new StringBuilder();
            sb.AppendLine("NodeType,Count,RawCaptured,CreatesGameObject,CreatesUnityComponent,IsInMaya2026StandardList");
            foreach (var r in result.NodeTypeRows)
            {
                var isStd = (standardOrNull != null && standardOrNull.Contains(r.NodeType));
                sb.Append(r.NodeType).Append(',')
                  .Append(r.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(r.RawCaptured ? "1" : "0").Append(',')
                  .Append(r.CreatesGameObject ? "1" : "0").Append(',')
                  .Append(r.CreatesUnityComponent ? "1" : "0").Append(',')
                  .Append(isStd ? "1" : "0")
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToUnknownCommandsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Command,Count,IsKnown");
            foreach (var kv in result.UnknownCommandCounts.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.Append(kv.Key).Append(',')
                  .Append(kv.Value.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append("0")
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToMissingStandardCsv(List<string> missing)
        {
            var sb = new StringBuilder();
            sb.AppendLine("MissingStandardNodeType");
            foreach (var t in missing) sb.AppendLine(t);
            return sb.ToString();
        }

        private static string ToMeshLimitationsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("MeshNodeName,IssueKey,Severity,Details");
            var list = result.MeshLimitations ?? new List<MayaMeshLimitationsReporter.MeshLimitationRow>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(Esc(r.MeshNodeName)).Append(',')
                  .Append(Esc(r.IssueKey)).Append(',')
                  .Append(Esc(r.Severity)).Append(',')
                  .Append(Esc(r.Details))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToSkinLimitationsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SkinClusterNodeName,IssueKey,Severity,Details");
            var list = result.SkinClusterLimitations ?? new List<MayaSkinClusterLimitationsReporter.SkinLimitationRow>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(Esc(r.SkinClusterNodeName)).Append(',')
                  .Append(Esc(r.IssueKey)).Append(',')
                  .Append(Esc(r.Severity)).Append(',')
                  .Append(Esc(r.Details))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToBlendLimitationsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("BlendShapeNodeName,IssueKey,Severity,Details");
            var list = result.BlendShapeLimitations ?? new List<MayaBlendShapeLimitationsReporter.BlendLimitationRow>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(Esc(r.BlendShapeNodeName)).Append(',')
                  .Append(Esc(r.IssueKey)).Append(',')
                  .Append(Esc(r.Severity)).Append(',')
                  .Append(Esc(r.Details))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToRigLimitationsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("NodeName,NodeType,IssueKey,Severity,Details");
            var list = result.RigLimitations ?? new List<MayaRigLimitationsReporter.RigLimitationRow>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(Esc(r.NodeName)).Append(',')
                  .Append(Esc(r.NodeType)).Append(',')
                  .Append(Esc(r.IssueKey)).Append(',')
                  .Append(Esc(r.Severity)).Append(',')
                  .Append(Esc(r.Details))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToAnimationLimitationsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scope,IssueKey,Severity,Details");
            var list = result.AnimationLimitations ?? new List<MayaAnimationEvaluationLimitationsReporter.AnimLimitationRow>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(Esc(r.Scope)).Append(',')
                  .Append(Esc(r.IssueKey)).Append(',')
                  .Append(Esc(r.Severity)).Append(',')
                  .Append(Esc(r.Details))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToShadingLimitationsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scope,NodeName,NodeType,IssueKey,Severity,Details");
            var list = result.ShadingLimitations ?? new List<MayaShadingNetworkLimitationsReporter.ShadingLimitationRow>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(Esc(r.Scope)).Append(',')
                  .Append(Esc(r.NodeName)).Append(',')
                  .Append(Esc(r.NodeType)).Append(',')
                  .Append(Esc(r.IssueKey)).Append(',')
                  .Append(Esc(r.Severity)).Append(',')
                  .Append(Esc(r.Details))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string ToDynamicsFxLimitationsCsv(CoverageResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Scope,NodeName,NodeType,IssueKey,Severity,Details");
            var list = result.DynamicsFxLimitations ?? new List<MayaDynamicsFxLimitationsReporter.DynamicsFxLimitationRow>();
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                sb.Append(Esc(r.Scope)).Append(',')
                  .Append(Esc(r.NodeName)).Append(',')
                  .Append(Esc(r.NodeType)).Append(',')
                  .Append(Esc(r.IssueKey)).Append(',')
                  .Append(Esc(r.Severity)).Append(',')
                  .Append(Esc(r.Details))
                  .AppendLine();
            }
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public sealed class CoverageResult
        {
            public List<NodeTypeRow> NodeTypeRows;
            public Dictionary<string, int> UnknownCommandCounts;

            public List<MayaMeshLimitationsReporter.MeshLimitationRow> MeshLimitations;
            public List<MayaSkinClusterLimitationsReporter.SkinLimitationRow> SkinClusterLimitations;
            public List<MayaBlendShapeLimitationsReporter.BlendLimitationRow> BlendShapeLimitations;
            public List<MayaRigLimitationsReporter.RigLimitationRow> RigLimitations;
            public List<MayaAnimationEvaluationLimitationsReporter.AnimLimitationRow> AnimationLimitations;
            public List<MayaShadingNetworkLimitationsReporter.ShadingLimitationRow> ShadingLimitations;

            // NEW
            public List<MayaDynamicsFxLimitationsReporter.DynamicsFxLimitationRow> DynamicsFxLimitations;
        }

        public sealed class NodeTypeRow
        {
            public string NodeType;
            public int Count;

            public bool RawCaptured;
            public bool CreatesGameObject;
            public bool CreatesUnityComponent;
        }
    }
}
