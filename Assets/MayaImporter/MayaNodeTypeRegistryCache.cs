// Assets/MayaImporter/Editor/MayaNodeTypeRegistryCache.cs
// Unity-only (no Maya/Autodesk API)
//
// Purpose:
// - Build a deterministic snapshot of "Maya nodeType -> Unity component" registry.
// - Detect duplicates (violates "1 nodeType = 1 script").
// - Compare against Maya2026 standard node types list (if available).
//
// This file is Editor-only utility code.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.EditorTools
{
    /// <summary>
    /// Deterministic, Editor-only view of the nodeType registry.
    /// Uses Unity's TypeCache for performance and stability.
    /// </summary>
    public static class MayaNodeTypeRegistryCache
    {
        public sealed class Snapshot
        {
            public readonly Dictionary<string, Type> PrimaryByNodeType;
            public readonly Dictionary<string, List<Type>> DuplicatesByNodeType;
            public readonly HashSet<string> AllNodeTypes;
            public readonly HashSet<string> Maya2026StandardNodeTypesOrNull;
            public readonly DateTime BuiltAtUtc;

            public Snapshot(
                Dictionary<string, Type> primary,
                Dictionary<string, List<Type>> dups,
                HashSet<string> all,
                HashSet<string> standardOrNull)
            {
                PrimaryByNodeType = primary;
                DuplicatesByNodeType = dups;
                AllNodeTypes = all;
                Maya2026StandardNodeTypesOrNull = standardOrNull;
                BuiltAtUtc = DateTime.UtcNow;
            }
        }

        private static Snapshot _cached;

        /// <summary>Invalidates in-memory cache (does not affect anything on disk).</summary>
        public static void Invalidate() => _cached = null;

        /// <summary>
        /// Build (or return cached) snapshot.
        /// </summary>
        public static Snapshot GetOrBuild(bool forceRebuild = false)
        {
            if (!forceRebuild && _cached != null) return _cached;

            // Load standard list if available
            HashSet<string> standard = null;
            if (MayaStandardNodeTypes.TryGet(out var set) && set != null && set.Count > 0)
                standard = new HashSet<string>(set, StringComparer.Ordinal);

            // Collect all derived types
            var derived = TypeCache.GetTypesDerivedFrom<MayaNodeComponentBase>();
            var byNodeType = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in derived)
            {
                if (t == null || t.IsAbstract) continue;

                var attrs = GetMayaNodeTypeAttributes(t);
                if (attrs == null || attrs.Count == 0) continue;

                foreach (var a in attrs)
                {
                    var nt = (a?.NodeType ?? "").Trim();
                    if (string.IsNullOrEmpty(nt)) continue;

                    if (!byNodeType.TryGetValue(nt, out var list))
                    {
                        list = new List<Type>(1);
                        byNodeType.Add(nt, list);
                    }

                    if (!list.Contains(t))
                        list.Add(t);
                }
            }

            // Build deterministic primary map + duplicates map
            var primary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var dups = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in byNodeType)
            {
                var nt = kv.Key;
                var list = kv.Value ?? new List<Type>();

                list.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

                all.Add(nt);

                if (list.Count == 0) continue;

                primary[nt] = list[0];

                if (list.Count > 1)
                    dups[nt] = new List<Type>(list);
            }

            _cached = new Snapshot(primary, dups, all, standard);
            return _cached;
        }

        /// <summary>
        /// Returns standard nodeTypes that are missing in the current codebase.
        /// If the standard list isn't available, returns an empty list.
        /// </summary>
        public static List<string> GetMissingStandardNodeTypes(bool forceRebuild = false)
        {
            var snap = GetOrBuild(forceRebuild);
            if (snap.Maya2026StandardNodeTypesOrNull == null || snap.Maya2026StandardNodeTypesOrNull.Count == 0)
                return new List<string>();

            var missing = snap.Maya2026StandardNodeTypesOrNull
                .Where(t => !snap.AllNodeTypes.Contains(t))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            return missing;
        }

        /// <summary>
        /// Returns a human-readable report text.
        /// </summary>
        public static string BuildReport(bool forceRebuild = false)
        {
            var snap = GetOrBuild(forceRebuild);

            int standardCount = snap.Maya2026StandardNodeTypesOrNull != null ? snap.Maya2026StandardNodeTypesOrNull.Count : 0;
            int missingCount = 0;

            if (standardCount > 0)
                missingCount = GetMissingStandardNodeTypes(forceRebuild: false).Count;

            var sb = new System.Text.StringBuilder(8 * 1024);
            sb.AppendLine("[MayaImporter] NodeType Registry Snapshot");
            sb.AppendLine("BuiltAt(UTC): " + snap.BuiltAtUtc.ToString("u"));
            sb.AppendLine("Mapped nodeTypes: " + snap.AllNodeTypes.Count);
            sb.AppendLine("Duplicate nodeTypes: " + snap.DuplicatesByNodeType.Count);
            sb.AppendLine("Maya2026 standard list: " + (standardCount > 0 ? standardCount.ToString() : "NOT FOUND"));
            if (standardCount > 0)
                sb.AppendLine("Missing standard nodeTypes: " + missingCount);

            if (snap.DuplicatesByNodeType.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("---- DUPLICATES (must fix for 1 nodeType = 1 script) ----");
                foreach (var kv in snap.DuplicatesByNodeType.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sb.Append("  ").Append(kv.Key).Append(" -> ");
                    sb.AppendLine(string.Join(", ", kv.Value.Select(t => t.FullName)));
                }
            }

            if (standardCount == 0)
            {
                sb.AppendLine();
                sb.AppendLine("NOTE: Put this file to enable 100% proof against Maya2026:");
                sb.AppendLine("  Assets/MayaImporter/Resources/Maya2026_StandardNodeTypes.txt");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Best-effort path to a MonoScript for a given type.
        /// Useful for opening/locating scripts in Editor.
        /// </summary>
        public static string TryGetScriptAssetPath(Type type)
        {
            if (type == null) return null;

            try
            {
                // Find by name first
                var guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
                if (guids == null || guids.Length == 0) return null;

                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
                    if (ms == null) continue;

                    var cls = ms.GetClass();
                    if (cls == type)
                        return p;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static List<MayaNodeTypeAttribute> GetMayaNodeTypeAttributes(Type t)
        {
            try
            {
                // Direct reference to attribute type (fast)
                var attrs = t.GetCustomAttributes(typeof(MayaNodeTypeAttribute), inherit: false);
                if (attrs == null || attrs.Length == 0) return null;

                var list = new List<MayaNodeTypeAttribute>(attrs.Length);
                foreach (var a in attrs)
                {
                    if (a is MayaNodeTypeAttribute m) list.Add(m);
                }
                return list;
            }
            catch (Exception)
            {
                // Fallback: name-based lookup (defensive)
                try
                {
                    var attrs = t.GetCustomAttributes(inherit: false);
                    if (attrs == null) return null;

                    var list = new List<MayaNodeTypeAttribute>();
                    foreach (var a in attrs)
                    {
                        if (a == null) continue;
                        if (!string.Equals(a.GetType().Name, "MayaNodeTypeAttribute", StringComparison.Ordinal))
                            continue;

                        var prop = a.GetType().GetProperty("NodeType", BindingFlags.Public | BindingFlags.Instance);
                        if (prop == null) continue;

                        var nt = prop.GetValue(a) as string;
                        if (string.IsNullOrEmpty(nt)) continue;

                        // create a temporary wrapper attribute instance
                        list.Add(new MayaNodeTypeAttribute(nt));
                    }
                    return list.Count > 0 ? list : null;
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
#endif
