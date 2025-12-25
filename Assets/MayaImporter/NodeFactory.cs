using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-1: Deterministic NodeFactory.
    /// Single source of truth: resolve by [MayaNodeType] attribute.
    ///
    /// IMPORTANT:
    /// We do NOT hard-reference MayaNodeTypeAttribute type here (namespace may vary).
    /// Instead we locate it via reflection by attribute name ("MayaNodeTypeAttribute")
    /// and read its "NodeType" string property.
    ///
    /// Phase-1 guarantee:
    /// - If duplicates exist, we still choose deterministically (by FullName ordinal sort),
    ///   and we log a single error line per duplicated maya nodeType.
    /// </summary>
    public static class NodeFactory
    {
        private const string MayaNodeTypeAttributeName = "MayaNodeTypeAttribute";
        private const string MayaNodeTypePropertyName = "NodeType";

        private static readonly Dictionary<string, Type> _nodeTypeMap =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _nodeTypeMap.Clear();

            var baseType = typeof(MayaNodeComponentBase);

            // 1) Collect all candidates first (so resolution does not depend on assembly/type scan order).
            var candidates = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

                for (int ti = 0; ti < types.Length; ti++)
                {
                    var type = types[ti];
                    if (type == null || type.IsAbstract) continue;
                    if (!baseType.IsAssignableFrom(type)) continue;

                    var mayaTypes = GetMayaNodeTypesFromAttributes(type);
                    if (mayaTypes == null || mayaTypes.Count == 0) continue;

                    for (int i = 0; i < mayaTypes.Count; i++)
                    {
                        var mayaType = mayaTypes[i];
                        if (string.IsNullOrEmpty(mayaType)) continue;

                        if (!candidates.TryGetValue(mayaType, out var list))
                        {
                            list = new List<Type>(1);
                            candidates[mayaType] = list;
                        }

                        if (!list.Contains(type))
                            list.Add(type);
                    }
                }
            }

            // 2) Resolve deterministically and report duplicates as errors.
            int duplicateKinds = 0;

            foreach (var kv in candidates)
            {
                var mayaType = kv.Key;
                var list = kv.Value;

                if (list == null || list.Count == 0)
                    continue;

                // Deterministic selection
                var chosen = list
                    .OrderBy(t => t.FullName, StringComparer.Ordinal)
                    .First();

                _nodeTypeMap[mayaType] = chosen;

                if (list.Count > 1)
                {
                    duplicateKinds++;
                    var ordered = list.OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
                    Debug.LogError(
                        $"[MayaImporter] Duplicate MayaNodeType '{mayaType}' mapped to {ordered.Length} types. " +
                        $"Chosen='{chosen.FullName}'. All=[{string.Join(", ", ordered.Select(t => t.FullName))}]");
                }
            }

            Debug.Log($"[MayaImporter] NodeFactory initialized: {_nodeTypeMap.Count} Maya node types. duplicates={duplicateKinds}");
        }

        public static MayaNodeComponentBase CreateComponent(GameObject target, string mayaNodeType)
        {
            if (!_initialized) Initialize();
            if (target == null) return null;

            if (string.IsNullOrEmpty(mayaNodeType))
                return target.AddComponent<MayaUnknownNodeComponent>();

            if (!_nodeTypeMap.TryGetValue(mayaNodeType, out var type) || type == null)
                return target.AddComponent<MayaUnknownNodeComponent>();

            var comp = target.AddComponent(type) as MayaNodeComponentBase;
            return comp != null ? comp : target.AddComponent<MayaUnknownNodeComponent>();
        }

        public static Type ResolveType(string mayaNodeType)
        {
            if (!_initialized) Initialize();
            if (string.IsNullOrEmpty(mayaNodeType)) return null;
            _nodeTypeMap.TryGetValue(mayaNodeType, out var t);
            return t;
        }

        public static IReadOnlyDictionary<string, Type> GetRegistry()
        {
            if (!_initialized) Initialize();
            return _nodeTypeMap;
        }

        // ---------- Attribute lookup (namespace-agnostic) ----------

        private static List<string> GetMayaNodeTypesFromAttributes(Type type)
        {
            // We only match attributes that are named "...MayaNodeTypeAttribute"
            // (full name includes namespace). This keeps it resilient.
            var attrs = type.GetCustomAttributes(inherit: false);
            if (attrs == null) return null;

            List<string> result = null;

            for (int i = 0; i < attrs.Length; i++)
            {
                var a = attrs[i];
                if (a == null) continue;

                var at = a.GetType();
                var name = at.Name; // "MayaNodeTypeAttribute"
                if (!string.Equals(name, MayaNodeTypeAttributeName, StringComparison.Ordinal))
                    continue;

                var prop = at.GetProperty(MayaNodeTypePropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || prop.PropertyType != typeof(string)) continue;

                var value = prop.GetValue(a) as string;
                if (string.IsNullOrEmpty(value)) continue;

                result ??= new List<string>();
                result.Add(value);
            }

            return result;
        }
    }
}
