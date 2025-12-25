using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MayaImporter.Runtime;

namespace MayaImporter.Core
{
    /// <summary>
    /// Build MayaSceneData -> Unity GameObjects.
    /// NOTE:
    /// - MayaTimeEvaluationPlayer が存在しないプロジェクトでもコンパイルできるように
    ///   直参照をやめてリフレクションで任意追加する。
    /// </summary>
    public sealed class UnitySceneBuilder
    {
        private readonly MayaImportOptions _options;
        private readonly MayaImportLog _log;

        public UnitySceneBuilder(MayaImportOptions options, MayaImportLog log)
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
                return root;

            // --- Phase-1 deterministic ordering ---
            var records = scene.Nodes.Values
                .Where(r => r != null && !string.IsNullOrEmpty(r.Name))
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            var goByName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var comps = new List<MayaNodeComponentBase>(records.Count);

            // 1) Create GameObjects + Components
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

                // materials
                MayaMaterialPostProcessor.Apply(root, scene, _options, _log);

                // runtime settings
                var settings = root.GetComponent<MayaSceneSettings>() ?? root.AddComponent<MayaSceneSettings>();
                settings.InitializeFrom(scene, _options);

                // ✅ MayaTimeEvaluationPlayer を直参照しない（存在する場合だけ付与）
                var player = EnsureComponentByName(root, "MayaImporter.Animation.MayaTimeEvaluationPlayer");
                if (player != null)
                    TrySetBoolMember(player, "loop", settings.loop);
            }
            finally
            {
                MayaBuildContext.Pop();
            }

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

        private static string GetLeaf(string mayaName)
        {
            if (string.IsNullOrEmpty(mayaName)) return "Node";
            var s = mayaName;
            var idx = s.LastIndexOf('|');
            if (idx >= 0 && idx + 1 < s.Length) s = s.Substring(idx + 1);
            return s;
        }

        // =========================================================
        // Reflection helpers (optional components)
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
            var t0 = Type.GetType(fullTypeName);
            if (t0 != null) return t0;

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
