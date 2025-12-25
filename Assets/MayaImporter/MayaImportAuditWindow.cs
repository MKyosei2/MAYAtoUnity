using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MayaImporter.Runtime;

namespace MayaImporter.Core
{
    /// <summary>
    /// V2: class-name changed to avoid duplicate UnitySceneBuilder in project.
    /// Also removes compile-time dependency on MayaTimeEvaluationPlayer (reflection attach).
    /// </summary>
    public sealed class UnitySceneBuilderV2
    {
        private readonly MayaImportOptions _options;
        private readonly MayaImportLog _log;

        public UnitySceneBuilderV2(MayaImportOptions options, MayaImportLog log)
        {
            _options = options ?? new MayaImportOptions();
            _log = log ?? new MayaImportLog();
        }

        public GameObject Build(MayaSceneData scene)
        {
            var root = new GameObject(
                string.IsNullOrEmpty(scene?.SourcePath)
                    ? "MayaScene"
                    : System.IO.Path.GetFileNameWithoutExtension(scene.SourcePath));

            if (scene == null || scene.Nodes == null || scene.Nodes.Count == 0)
            {
                AttachRootInfo(root, scene, _log);
                return root;
            }

            // --- deterministic ordering ---
            var records = scene.Nodes.Values
                .Where(r => r != null && !string.IsNullOrEmpty(r.Name))
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            var goByName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var comps = new List<MayaNodeComponentBase>(records.Count);

            // 1) Create GameObjects + Components (1 Maya node => 1 GO + 1 Component)
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];

                var go = new GameObject(GetLeaf(rec.Name));
                goByName[rec.Name] = go;

                var comp = NodeFactory.CreateComponent(go, rec.NodeType);
                if (comp == null) comp = go.AddComponent<MayaUnknownNodeComponent>();

                comp.InitializeFromRecord(rec, scene.Connections);
                comps.Add(comp);
            }

            // 2) Parenting
            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (!goByName.TryGetValue(rec.Name, out var go)) continue;

                Transform parentTr = root.transform;

                if (!string.IsNullOrEmpty(rec.ParentName) && goByName.TryGetValue(rec.ParentName, out var parentGo))
                    parentTr = parentGo.transform;

                go.transform.SetParent(parentTr, false);
            }

            // 3) Scene-wide context during ApplyToUnity
            MayaBuildContext.Push(scene, _options, _log);

            try
            {
                // 3) ApplyToUnity in stable glayersh.
                var buckets = new SortedDictionary<int, List<MayaNodeComponentBase>>();

                for (int i = 0; i < comps.Count; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;

                    int stage = GetStagePriority(c.NodeType);

                    if (!buckets.TryGetValue(stage, out var list))
                    {
                        list = new List<MayaNodeComponentBase>(32);
                        buckets.Add(stage, list);
                    }

                    list.Add(c);
                }

                foreach (var kv in buckets)
                {
                    var list = kv.Value;

                    // Stable within a stage: depth -> nodeType -> nodeName
                    list.Sort(CompareWithinStage);

                    for (int i = 0; i < list.Count; i++)
                    {
                        var c = list[i];
                        try
                        {
                            c.ApplyToUnity(_options, _log);
                        }
                        catch (Exception e)
                        {
                            _log.Warn($"[ApplyToUnity] {c.NodeName} ({c.NodeType}) {e.GetType().Name}: {e.Message}");
                        }
                    }
                }

                // Phase-B: materials are finalized AFTER all nodes are applied.
                MayaMaterialPostProcessor.Apply(root, scene, _options, _log);

                // Scene settings
                var settings = root.GetComponent<MayaSceneSettings>() ?? root.AddComponent<MayaSceneSettings>();
                settings.InitializeFrom(scene, _options);

                // Optional runtime evaluation player (no compile-time dependency)
                var player = EnsureComponentByName(root, "MayaImporter.Animation.MayaTimeEvaluationPlayer");
                if (player != null)
                    TrySetBoolMember(player, "loop", settings.loop);
            }
            finally
            {
                MayaBuildContext.Pop();
            }

            // Portfolio / Audit proof
            AttachRootInfo(root, scene, _log);

            return root;
        }

        // ---- Deterministic staging ----
        private static int GetStagePriority(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return 900;

            if (Eq(nodeType, "transform") || Eq(nodeType, "joint")) return 0;

            if (Eq(nodeType, "camera")) return 10;
            if (nodeType.EndsWith("Light", StringComparison.OrdinalIgnoreCase)) return 10;

            if (Eq(nodeType, "mesh") || Eq(nodeType, "nurbsCurve") || Eq(nodeType, "nurbsSurface")) return 20;

            if (Eq(nodeType, "blendShape") || Eq(nodeType, "skinCluster")) return 30;

            if (nodeType.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
            if (nodeType.IndexOf("ik", StringComparison.OrdinalIgnoreCase) >= 0) return 40;
            if (nodeType.IndexOf("motionPath", StringComparison.OrdinalIgnoreCase) >= 0) return 40;

            if (Eq(nodeType, "shadingEngine")) return 60;
            if (LooksLikeShaderOrTextureNode(nodeType)) return 50;

            if (nodeType.StartsWith("animCurve", StringComparison.OrdinalIgnoreCase)) return 70;
            if (nodeType.IndexOf("anim", StringComparison.OrdinalIgnoreCase) >= 0) return 70;

            return 800;
        }

        private static int CompareWithinStage(MayaNodeComponentBase a, MayaNodeComponentBase b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            int da = GetDepth(a.transform);
            int db = GetDepth(b.transform);
            if (da != db) return da.CompareTo(db);

            var ta = a.NodeType ?? "";
            var tb = b.NodeType ?? "";
            int ct = StringComparer.OrdinalIgnoreCase.Compare(ta, tb);
            if (ct != 0) return ct;

            var na = a.NodeName ?? "";
            var nb = b.NodeName ?? "";
            return StringComparer.Ordinal.Compare(na, nb);
        }

        private static bool LooksLikeShaderOrTextureNode(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;

            if (Eq(nodeType, "file")) return true;
            if (Eq(nodeType, "place2dTexture")) return true;

            if (nodeType.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("surface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("texture", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("bump", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("ramp", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("remap", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("aiStandardSurface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("standardSurface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (Eq(nodeType, "lambert") || Eq(nodeType, "phong") || Eq(nodeType, "blinn")) return true;

            return false;
        }

        private static bool Eq(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int GetDepth(Transform t)
        {
            int d = 0;
            while (t != null && t.parent != null)
            {
                d++;
                t = t.parent;
            }
            return d;
        }

        private static string GetLeaf(string dag)
        {
            if (string.IsNullOrEmpty(dag)) return "node";
            int idx = dag.LastIndexOf('|');
            return (idx >= 0 && idx < dag.Length - 1) ? dag.Substring(idx + 1) : dag;
        }

        // =========================================================
        // Portfolio/Audit proof component attach
        // =========================================================

        private static void AttachRootInfo(GameObject root, MayaSceneData scene, MayaImportLog log)
        {
            if (root == null) return;

            var info = root.GetComponent<MayaImportedRootInfo>();
            if (info == null) info = root.AddComponent<MayaImportedRootInfo>();

            info.sourcePath = scene?.SourcePath ?? "";
            info.sourceKind = scene != null ? scene.SourceKind.ToString() : "Unknown";
            info.schemaVersion = scene != null ? scene.SchemaVersion : 0;
            info.rawSha256 = scene?.RawSha256 ?? "";

            info.nodeCount = scene?.Nodes?.Count ?? 0;
            info.connectionCount = scene?.Connections?.Count ?? 0;

            if (scene != null && scene.SourceKind == MayaSourceKind.BinaryMb)
            {
                info.mbExtractedAsciiStatements = scene.MbExtractedAsciiStatementCount;
                info.mbExtractedAsciiScore = scene.MbExtractedAsciiConfidence;
                info.mbExtractedAsciiChars = string.IsNullOrEmpty(scene.MbExtractedAsciiText) ? 0 : scene.MbExtractedAsciiText.Length;
            }
            else
            {
                info.mbExtractedAsciiStatements = 0;
                info.mbExtractedAsciiScore = 0;
                info.mbExtractedAsciiChars = 0;
            }

            var allNodeComps = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            info.unityNodeComponentCount = allNodeComps != null ? allNodeComps.Length : 0;

            int unknown = 0;
            if (allNodeComps != null)
            {
                for (int i = 0; i < allNodeComps.Length; i++)
                {
                    if (allNodeComps[i] is MayaUnknownNodeComponent) unknown++;
                }
            }
            info.unknownNodeComponentCount = unknown;

            var opaque = root.GetComponentsInChildren<MayaOpaqueNodeRuntime>(true);
            info.opaqueRuntimeNodeCount = opaque != null ? opaque.Length : 0;

            info.warningCount = log?.Warnings?.Count ?? 0;
            info.errorCount = log?.Errors?.Count ?? 0;

            if (scene != null)
            {
                var typeCounts = scene.CountNodeTypes();
                var top = typeCounts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Take(12)
                    .ToList();

                info.topNodeTypes = top.Select(x => x.Key).ToArray();
                info.topNodeTypeCounts = top.Select(x => x.Value).ToArray();
            }
            else
            {
                info.topNodeTypes = Array.Empty<string>();
                info.topNodeTypeCounts = Array.Empty<int>();
            }

            info.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
        }

        // =========================================================
        // Reflection helpers (avoid hard dependency)
        // =========================================================

        private static Component EnsureComponentByName(GameObject go, string fullTypeName)
        {
            if (go == null || string.IsNullOrEmpty(fullTypeName)) return null;

            var t = FindType(fullTypeName);
            if (t == null) return null;

            var existing = go.GetComponent(t);
            return existing != null ? existing : go.AddComponent(t);
        }

        private static Type FindType(string fullTypeName)
        {
            // Try direct
            var t0 = Type.GetType(fullTypeName);
            if (t0 != null) return t0;

            // Search loaded assemblies
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                var t = asms[i].GetType(fullTypeName);
                if (t != null) return t;
            }
            return null;
        }

        private static void TrySetBoolMember(Component c, string memberName, bool value)
        {
            if (c == null || string.IsNullOrEmpty(memberName)) return;

            var t = c.GetType();

            var p = t.GetProperty(memberName);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool))
            {
                p.SetValue(c, value);
                return;
            }

            var f = t.GetField(memberName);
            if (f != null && f.FieldType == typeof(bool))
            {
                f.SetValue(c, value);
            }
        }
    }
}
