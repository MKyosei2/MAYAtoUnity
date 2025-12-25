// Assets/MayaImporter/MayaNodeAudit.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    public static class MayaNodeAudit
    {
        // “nodeTypeに紐づかない受け皿”なので [MayaNodeType] 無くてOK扱いにする
        private static readonly HashSet<string> NoNodeTypeRequired_FullNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "MayaImporter.Core.MayaPlaceholderNode",
            "MayaImporter.Core.MayaUnknownNodeComponent",
            "MayaImporter.Shader.UnknownShaderNodeComponent",
        };

        [MenuItem("Tools/Maya Importer/Run Node Audit")]
        public static void RunAudit()
        {
            var baseType = typeof(MayaNodeComponentBase);

            // --- types ---
            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
                })
                .Where(t => t != null && !t.IsAbstract)
                .ToArray();

            // --- scan ---
            var nodeTypeToImpl = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
            var invalidAttrUsers = new List<Type>();      // has [MayaNodeType] but not derives base
            var derivedButNoAttr = new List<Type>();      // derives base but missing [MayaNodeType] (excluding allowed)
            var duplicates = new List<string>();          // duplicate nodeType lines

            foreach (var t in allTypes)
            {
                bool derivesBase = baseType.IsAssignableFrom(t);
                var attrs = t.GetCustomAttributes(typeof(MayaNodeTypeAttribute), inherit: false);
                bool hasAttr = attrs != null && attrs.Length > 0;

                if (!derivesBase && hasAttr)
                {
                    invalidAttrUsers.Add(t);
                    continue;
                }

                if (derivesBase)
                {
                    if (!hasAttr)
                    {
                        // allowed exceptions
                        if (!NoNodeTypeRequired_FullNames.Contains(t.FullName))
                            derivedButNoAttr.Add(t);
                        continue;
                    }

                    foreach (var a in attrs)
                    {
                        var nt = ((MayaNodeTypeAttribute)a).NodeType;
                        if (string.IsNullOrEmpty(nt))
                            continue;

                        if (!nodeTypeToImpl.TryGetValue(nt, out var list))
                        {
                            list = new List<Type>(1);
                            nodeTypeToImpl[nt] = list;
                        }
                        list.Add(t);
                    }
                }
            }

            foreach (var kv in nodeTypeToImpl)
            {
                if (kv.Value.Count > 1)
                    duplicates.Add($"{kv.Key}: {string.Join(", ", kv.Value.Select(x => x.FullName))}");
            }

            // --- report ---
            var issues = new List<string>();

            issues.Add($"[MayaImporter] Node Audit: Types scanned={allTypes.Length}, " +
                      $"[MayaNodeType] implementations={nodeTypeToImpl.Sum(k => k.Value.Count)}, " +
                      $"duplicates={duplicates.Count}, invalid-attr-users={invalidAttrUsers.Count}, " +
                      $"derived-but-no-attr={derivedButNoAttr.Count}");

            if (derivedButNoAttr.Count > 0)
            {
                issues.Add("❌ MayaNodeComponentBase-derived classes missing [MayaNodeType] (excluding allowed fallbacks):");
                foreach (var t in derivedButNoAttr.OrderBy(x => x.FullName))
                    issues.Add($"   - {t.FullName}");
            }

            if (duplicates.Count > 0)
            {
                issues.Add("⚠ Duplicate nodeType mappings:");
                foreach (var d in duplicates.OrderBy(x => x))
                    issues.Add($"   - '{d}'");
            }

            if (invalidAttrUsers.Count > 0)
            {
                issues.Add("⚠ [MayaNodeType] attribute used on non-MayaNodeComponentBase types:");
                foreach (var t in invalidAttrUsers.OrderBy(x => x.FullName))
                    issues.Add($"   - {t.FullName}");
            }

            if (issues.Count == 1)
            {
                issues.Add("✅ No issues found.");
            }

            Debug.Log("<color=yellow>[MayaImporter] Node Audit found " + issues.Count + " lines:</color>\n" +
                      string.Join("\n", issues));
        }
    }
}
#endif
