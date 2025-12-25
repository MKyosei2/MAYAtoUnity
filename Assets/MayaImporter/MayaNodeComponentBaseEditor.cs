#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    /// <summary>
    /// Phase-2 proof UX:
    /// Make every Maya node (even "unsupported" ones) verifiably reconstructible in Unity by showing:
    /// - identity
    /// - raw attributes (lossless tokens) with search
    /// - related connections with search + ping target
    /// </summary>
    [CustomEditor(typeof(MayaNodeComponentBase), true)]
    public sealed class MayaNodeComponentBaseEditor : UnityEditor.Editor
    {
        private bool _foldIdentity = true;
        private bool _foldAttrs = true;
        private bool _foldConns = true;

        private string _attrSearch = "";
        private string _connSearch = "";

        private int _attrLimit = 200;
        private int _connLimit = 200;

        private static readonly GUIContent GC_Copy = new GUIContent("Copy");
        private static readonly GUIContent GC_Ping = new GUIContent("Ping");
        private static readonly GUIContent GC_Log = new GUIContent("Log");
        private static readonly GUIContent GC_CopyJson = new GUIContent("Copy JSON");

        public override void OnInspectorGUI()
        {
            var n = target as MayaNodeComponentBase;
            if (n == null)
            {
                base.OnInspectorGUI();
                return;
            }

            DrawHeader(n);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _foldIdentity = EditorGUILayout.Foldout(_foldIdentity, "Identity", true);
                if (_foldIdentity)
                    DrawIdentity(n);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _foldAttrs = EditorGUILayout.Foldout(_foldAttrs, $"Attributes (raw)  [{(n.Attributes != null ? n.Attributes.Count : 0)}]", true);
                if (_foldAttrs)
                    DrawAttributes(n);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _foldConns = EditorGUILayout.Foldout(_foldConns, $"Connections (related)  [{(n.Connections != null ? n.Connections.Count : 0)}]", true);
                if (_foldConns)
                    DrawConnections(n);
            }

            EditorGUILayout.Space(6);

            // Show default serialized fields if any subclass adds its own
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Subclass Fields (if any)", EditorStyles.boldLabel);
                DrawDefaultInspectorExceptBase(n);
            }
        }

        private void DrawHeader(MayaNodeComponentBase n)
        {
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Maya Node: {n.NodeType}", EditorStyles.boldLabel);

                if (GUILayout.Button(GC_Log, GUILayout.Width(48)))
                {
                    Debug.Log(BuildSummary(n));
                }

                if (GUILayout.Button(GC_CopyJson, GUILayout.Width(88)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildJson(n);
                    Debug.Log("[MayaImporter] Copied node JSON to clipboard.");
                }
            }

            EditorGUILayout.LabelField(n.NodeName ?? "(null)", EditorStyles.miniBoldLabel);

            EditorGUILayout.Space(2);
        }

        private void DrawIdentity(MayaNodeComponentBase n)
        {
            DrawCopyableLine("NodeName", n.NodeName);
            DrawCopyableLine("NodeType", n.NodeType);
            DrawCopyableLine("ParentName", n.ParentName);
            DrawCopyableLine("UUID", n.Uuid);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Unity Path", GUILayout.Width(90));
                var path = GetTransformPath(n.transform);
                EditorGUILayout.SelectableLabel(path, GUILayout.Height(EditorGUIUtility.singleLineHeight));

                if (GUILayout.Button(GC_Copy, GUILayout.Width(48)))
                    EditorGUIUtility.systemCopyBuffer = path;
            }
        }

        private void DrawAttributes(MayaNodeComponentBase n)
        {
            _attrSearch = DrawSearchBar("Search", _attrSearch);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Show up to", GUILayout.Width(80));
                _attrLimit = Mathf.Clamp(EditorGUILayout.IntField(_attrLimit, GUILayout.Width(80)), 10, 5000);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy Visible", GUILayout.Width(92)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildVisibleAttributesText(n, _attrSearch, _attrLimit);
                    Debug.Log("[MayaImporter] Copied visible Attributes to clipboard.");
                }
            }

            var attrs = n.Attributes ?? new System.Collections.Generic.List<MayaNodeComponentBase.SerializedAttribute>();
            if (attrs.Count == 0)
            {
                EditorGUILayout.HelpBox("No attributes stored on this node.", MessageType.Info);
                return;
            }

            var filtered = attrs
                .Where(a => a != null)
                .OrderBy(a => a.Key ?? "", StringComparer.Ordinal)
                .Where(a =>
                {
                    if (string.IsNullOrEmpty(_attrSearch)) return true;
                    var s = _attrSearch;
                    return (a.Key != null && a.Key.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (a.TypeName != null && a.TypeName.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (a.Tokens != null && a.Tokens.Any(t => t != null && t.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));
                })
                .Take(_attrLimit)
                .ToArray();

            EditorGUILayout.LabelField($"Visible: {filtered.Length} / {attrs.Count}", EditorStyles.miniLabel);

            using (new EditorGUILayout.VerticalScope())
            {
                for (int i = 0; i < filtered.Length; i++)
                {
                    var a = filtered[i];
                    DrawAttributeRow(a);
                }
            }
        }

        private void DrawAttributeRow(MayaNodeComponentBase.SerializedAttribute a)
        {
            var key = a.Key ?? "";
            var type = a.TypeName ?? "";
            var tokens = (a.Tokens != null && a.Tokens.Count > 0) ? string.Join(" ", a.Tokens) : "";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(key, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    if (!string.IsNullOrEmpty(type))
                        EditorGUILayout.LabelField(type, EditorStyles.miniLabel, GUILayout.Width(140));

                    if (GUILayout.Button(GC_Copy, GUILayout.Width(48)))
                        EditorGUIUtility.systemCopyBuffer = $"{key} ({type}) {tokens}";
                }

                if (!string.IsNullOrEmpty(tokens))
                    EditorGUILayout.SelectableLabel(tokens, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
                else
                    EditorGUILayout.LabelField("(no tokens)", EditorStyles.miniLabel);
            }
        }

        private void DrawConnections(MayaNodeComponentBase n)
        {
            _connSearch = DrawSearchBar("Search", _connSearch);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Show up to", GUILayout.Width(80));
                _connLimit = Mathf.Clamp(EditorGUILayout.IntField(_connLimit, GUILayout.Width(80)), 10, 5000);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy Visible", GUILayout.Width(92)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildVisibleConnectionsText(n, _connSearch, _connLimit);
                    Debug.Log("[MayaImporter] Copied visible Connections to clipboard.");
                }
            }

            var conns = n.Connections ?? new System.Collections.Generic.List<MayaNodeComponentBase.SerializedConnection>();
            if (conns.Count == 0)
            {
                EditorGUILayout.HelpBox("No related connections stored on this node.", MessageType.Info);
                return;
            }

            var filtered = conns
                .Where(c => c != null)
                .OrderBy(c => c.SrcPlug ?? "", StringComparer.Ordinal)
                .ThenBy(c => c.DstPlug ?? "", StringComparer.Ordinal)
                .Where(c =>
                {
                    if (string.IsNullOrEmpty(_connSearch)) return true;
                    var s = _connSearch;
                    return (c.SrcPlug != null && c.SrcPlug.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (c.DstPlug != null && c.DstPlug.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (c.SrcNodePart != null && c.SrcNodePart.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (c.DstNodePart != null && c.DstNodePart.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
                })
                .Take(_connLimit)
                .ToArray();

            EditorGUILayout.LabelField($"Visible: {filtered.Length} / {conns.Count}", EditorStyles.miniLabel);

            for (int i = 0; i < filtered.Length; i++)
            {
                var c = filtered[i];
                DrawConnectionRow(c);
            }
        }

        private void DrawConnectionRow(MayaNodeComponentBase.SerializedConnection c)
        {
            var src = c.SrcPlug ?? "";
            var dst = c.DstPlug ?? "";
            var role = c.RoleForThisNode.ToString();
            var force = c.Force ? "force" : "normal";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{role} / {force}", EditorStyles.miniBoldLabel, GUILayout.Width(120));

                    if (GUILayout.Button(GC_Copy, GUILayout.Width(48)))
                        EditorGUIUtility.systemCopyBuffer = $"{src} -> {dst} ({role}, {force})";

                    // Try ping the opposite side transform (best-effort)
                    var other = GuessOtherNodeName(c);
                    var t = MayaNodeLookup.FindTransform(other);
                    using (new EditorGUI.DisabledScope(t == null))
                    {
                        if (GUILayout.Button(GC_Ping, GUILayout.Width(48)))
                            EditorGUIUtility.PingObject(t.gameObject);
                    }
                }

                EditorGUILayout.SelectableLabel($"{src}  Å®  {dst}", GUILayout.MinHeight(EditorGUIUtility.singleLineHeight));
                if (!string.IsNullOrEmpty(c.SrcNodePart) || !string.IsNullOrEmpty(c.DstNodePart))
                    EditorGUILayout.LabelField($"SrcNode: {c.SrcNodePart} | DstNode: {c.DstNodePart}", EditorStyles.miniLabel);
            }
        }

        private string GuessOtherNodeName(MayaNodeComponentBase.SerializedConnection c)
        {
            // If this node is Source, other is DstNodePart; if Destination, other is SrcNodePart
            // If Both/Unknown, pick whichever exists.
            if (c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Source)
                return c.DstNodePart;
            if (c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Destination)
                return c.SrcNodePart;

            if (!string.IsNullOrEmpty(c.SrcNodePart)) return c.SrcNodePart;
            if (!string.IsNullOrEmpty(c.DstNodePart)) return c.DstNodePart;
            return null;
        }

        private static string DrawSearchBar(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(50));
                value = EditorGUILayout.TextField(value);

                if (GUILayout.Button("Clear", GUILayout.Width(52)))
                    value = "";
            }
            return value ?? "";
        }

        private static void DrawCopyableLine(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(90));
                EditorGUILayout.SelectableLabel(value ?? "", GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button(GC_Copy, GUILayout.Width(48)))
                    EditorGUIUtility.systemCopyBuffer = value ?? "";
            }
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "";
            var sb = new StringBuilder(256);
            while (t != null)
            {
                if (sb.Length == 0) sb.Insert(0, t.name);
                else sb.Insert(0, t.name + "/");
                t = t.parent;
            }
            return sb.ToString();
        }

        private static string BuildSummary(MayaNodeComponentBase n)
        {
            var attrs = n.Attributes?.Count ?? 0;
            var conns = n.Connections?.Count ?? 0;
            return $"[MayaNode] Type={n.NodeType} Name={n.NodeName} Parent={n.ParentName} UUID={n.Uuid} Attrs={attrs} Conns={conns}";
        }

        private static string BuildVisibleAttributesText(MayaNodeComponentBase n, string search, int limit)
        {
            var attrs = n.Attributes ?? new System.Collections.Generic.List<MayaNodeComponentBase.SerializedAttribute>();
            var filtered = attrs
                .Where(a => a != null)
                .OrderBy(a => a.Key ?? "", StringComparer.Ordinal)
                .Where(a =>
                {
                    if (string.IsNullOrEmpty(search)) return true;
                    return (a.Key != null && a.Key.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (a.TypeName != null && a.TypeName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (a.Tokens != null && a.Tokens.Any(t => t != null && t.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
                })
                .Take(limit);

            var sb = new StringBuilder();
            foreach (var a in filtered)
            {
                var tokens = (a.Tokens != null && a.Tokens.Count > 0) ? string.Join(" ", a.Tokens) : "";
                sb.AppendLine($"{a.Key} ({a.TypeName}) {tokens}");
            }
            return sb.ToString();
        }

        private static string BuildVisibleConnectionsText(MayaNodeComponentBase n, string search, int limit)
        {
            var conns = n.Connections ?? new System.Collections.Generic.List<MayaNodeComponentBase.SerializedConnection>();
            var filtered = conns
                .Where(c => c != null)
                .OrderBy(c => c.SrcPlug ?? "", StringComparer.Ordinal)
                .ThenBy(c => c.DstPlug ?? "", StringComparer.Ordinal)
                .Where(c =>
                {
                    if (string.IsNullOrEmpty(search)) return true;
                    return (c.SrcPlug != null && c.SrcPlug.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (c.DstPlug != null && c.DstPlug.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (c.SrcNodePart != null && c.SrcNodePart.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (c.DstNodePart != null && c.DstNodePart.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                })
                .Take(limit);

            var sb = new StringBuilder();
            foreach (var c in filtered)
                sb.AppendLine($"{c.SrcPlug} -> {c.DstPlug} (role={c.RoleForThisNode}, force={c.Force})");
            return sb.ToString();
        }

        private string BuildJson(MayaNodeComponentBase n)
        {
            // Minimal JSON (no external JSON lib)
            var sb = new StringBuilder(4096);
            sb.Append("{");
            AppendJsonKV(sb, "NodeName", n.NodeName); sb.Append(",");
            AppendJsonKV(sb, "NodeType", n.NodeType); sb.Append(",");
            AppendJsonKV(sb, "ParentName", n.ParentName); sb.Append(",");
            AppendJsonKV(sb, "Uuid", n.Uuid); sb.Append(",");

            sb.Append("\"Attributes\":[");
            var attrs = n.Attributes ?? new System.Collections.Generic.List<MayaNodeComponentBase.SerializedAttribute>();
            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null) continue;
                if (i > 0) sb.Append(",");
                sb.Append("{");
                AppendJsonKV(sb, "Key", a.Key); sb.Append(",");
                AppendJsonKV(sb, "TypeName", a.TypeName); sb.Append(",");
                sb.Append("\"Tokens\":[");
                if (a.Tokens != null)
                {
                    for (int k = 0; k < a.Tokens.Count; k++)
                    {
                        if (k > 0) sb.Append(",");
                        sb.Append("\"").Append(EscapeJson(a.Tokens[k] ?? "")).Append("\"");
                    }
                }
                sb.Append("]}");
            }
            sb.Append("],");

            sb.Append("\"Connections\":[");
            var conns = n.Connections ?? new System.Collections.Generic.List<MayaNodeComponentBase.SerializedConnection>();
            for (int i = 0; i < conns.Count; i++)
            {
                var c = conns[i];
                if (c == null) continue;
                if (i > 0) sb.Append(",");
                sb.Append("{");
                AppendJsonKV(sb, "SrcPlug", c.SrcPlug); sb.Append(",");
                AppendJsonKV(sb, "DstPlug", c.DstPlug); sb.Append(",");
                sb.Append("\"Force\":").Append(c.Force ? "true" : "false").Append(",");
                AppendJsonKV(sb, "RoleForThisNode", c.RoleForThisNode.ToString()); sb.Append(",");
                AppendJsonKV(sb, "SrcNodePart", c.SrcNodePart); sb.Append(",");
                AppendJsonKV(sb, "DstNodePart", c.DstNodePart);
                sb.Append("}");
            }
            sb.Append("]");

            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendJsonKV(StringBuilder sb, string key, string value)
        {
            sb.Append("\"").Append(EscapeJson(key)).Append("\":");
            sb.Append("\"").Append(EscapeJson(value ?? "")).Append("\"");
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private void DrawDefaultInspectorExceptBase(MayaNodeComponentBase n)
        {
            // We want to avoid drawing base fields twice.
            // So we draw only serialized properties not belonging to MayaNodeComponentBase.
            serializedObject.Update();

            var p = serializedObject.GetIterator();
            bool enter = true;

            while (p.NextVisible(enter))
            {
                enter = false;

                if (p.name == "m_Script") // always show script field
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(p, true);
                    continue;
                }

                // Skip base fields
                if (p.name == nameof(MayaNodeComponentBase.NodeName)) continue;
                if (p.name == nameof(MayaNodeComponentBase.NodeType)) continue;
                if (p.name == nameof(MayaNodeComponentBase.ParentName)) continue;
                if (p.name == nameof(MayaNodeComponentBase.Uuid)) continue;
                if (p.name == nameof(MayaNodeComponentBase.Attributes)) continue;
                if (p.name == nameof(MayaNodeComponentBase.Connections)) continue;

                EditorGUILayout.PropertyField(p, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
