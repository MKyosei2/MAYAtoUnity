// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Deterministic NodeFactory:
    /// - Resolves Maya nodeType -> Unity component type by [MayaNodeType] attribute.
    /// - Unity-only (no Maya/Autodesk API).
    ///
    /// STRICT POLICY (portfolio-grade):
    /// - 1 nodeType = 1 script (= 1 concrete component type)
    /// - In Editor, duplicates cause FAIL FAST so issues are fixed at the source.
    /// - In non-Editor builds, duplicates are logged and resolved deterministically.
    /// </summary>
    public static class NodeFactory
    {
        private const string MayaNodeTypeAttributeName = "MayaNodeTypeAttribute";
        private const string MayaNodeTypePropertyName = "NodeType";

#if UNITY_EDITOR
        private const bool StrictFailOnDuplicate = true;
#else
        private const bool StrictFailOnDuplicate = false;
#endif

        private static readonly Dictionary<string, Type> _nodeTypeMap =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, List<Type>> _duplicateMap =
            new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;
        private static bool _initializing;

        private static void Initialize()
        {
            if (_initialized || _initializing) return;
            _initializing = true;

            try
            {
                _nodeTypeMap.Clear();
                _duplicateMap.Clear();

                var baseType = typeof(MayaNodeComponentBase);

                // 1) Collect all candidates (scan order independent).
                var candidates = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { continue; }

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
                            if (string.IsNullOrWhiteSpace(mayaType)) continue;

                            mayaType = mayaType.Trim();

                            // Guard against placeholder strings that sometimes appear in generator docs.
                            if (mayaType == "..." || mayaType == "xxx")
                            {
                                Debug.LogError($"[MayaImporter] Invalid nodeType placeholder detected on type '{type.FullName}'. Remove it. nodeType='{mayaType}'");
                                continue;
                            }

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

                // 2) Detect duplicates and (optionally) fail.
                int duplicateKinds = 0;

                foreach (var kv in candidates)
                {
                    var mayaType = kv.Key;
                    var list = kv.Value;
                    if (list == null || list.Count == 0) continue;

                    if (list.Count > 1)
                    {
                        duplicateKinds++;
                        var ordered = list.OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
                        _duplicateMap[mayaType] = ordered;

                        Debug.LogError(
                            $"[MayaImporter] DUPLICATE nodeType mapping detected: '{mayaType}' -> {ordered.Count} types. " +
                            $"All=[{string.Join(", ", ordered.Select(t => t.FullName))}]");
                    }
                }

                if (duplicateKinds > 0 && StrictFailOnDuplicate)
                {
                    throw new InvalidOperationException(
                        $"[MayaImporter] NodeFactory STRICT FAIL: duplicate nodeType mappings detected: {duplicateKinds}. " +
                        "Fix duplicates to satisfy '1 node = 1 script'. See Console for details.");
                }

                // 3) Build final map (deterministic).
                foreach (var kv in candidates)
                {
                    var mayaType = kv.Key;
                    var list = kv.Value;
                    if (list == null || list.Count == 0) continue;

                    var chosen = list
                        .OrderBy(t => t.FullName, StringComparer.Ordinal)
                        .First();

                    _nodeTypeMap[mayaType] = chosen;

                    if (list.Count > 1 && !StrictFailOnDuplicate)
                    {
                        Debug.LogError(
                            $"[MayaImporter] Duplicate nodeType '{mayaType}' resolved deterministically. " +
                            $"Chosen='{chosen.FullName}'. (Editor strict mode is OFF in this build)");
                    }
                }

                _initialized = true;
                Debug.Log($"[MayaImporter] NodeFactory initialized: {_nodeTypeMap.Count} nodeTypes, duplicates={duplicateKinds}, strict={(StrictFailOnDuplicate ? "ON" : "OFF")}");
            }
            finally
            {
                _initializing = false;
            }
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

        /// <summary>
        /// Returns duplicate mappings detected during initialization.
        /// Empty means PASS for "1 nodeType = 1 script".
        /// </summary>
        public static IReadOnlyDictionary<string, List<Type>> GetDuplicateNodeTypes()
        {
            if (!_initialized) Initialize();
            return _duplicateMap;
        }

        // ---------- Attribute lookup (namespace-agnostic) ----------

        private static List<string> GetMayaNodeTypesFromAttributes(Type type)
        {
            var attrs = type.GetCustomAttributes(inherit: false);
            if (attrs == null) return null;

            List<string> result = null;

            for (int i = 0; i < attrs.Length; i++)
            {
                var a = attrs[i];
                if (a == null) continue;

                var at = a.GetType();
                if (!string.Equals(at.Name, MayaNodeTypeAttributeName, StringComparison.Ordinal))
                    continue;

                var prop = at.GetProperty(MayaNodeTypePropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || prop.PropertyType != typeof(string)) continue;

                var value = prop.GetValue(a) as string;
                if (string.IsNullOrWhiteSpace(value)) continue;

                result ??= new List<string>();
                result.Add(value.Trim());
            }

            return result;
        }

        // --- Node category helpers (for scene reconstruction policy) ---
        public static bool IsDagObjectNodeType(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;
            nodeType = nodeType.Trim();
            // Core DAG-ish objects
            switch (nodeType)
            {
                case "transform":
                case "joint":
                case "locator":
                case "ikHandle":
                case "ikEffector":
                case "dagPose":
                case "groupId":
                case "groupParts":
                    return true;
            }
            // Common solver transforms that often behave like DAG nodes
            if (nodeType.EndsWith("Transform", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public static bool IsCameraOrLightNodeType(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;
            nodeType = nodeType.Trim();
            if (string.Equals(nodeType, "camera", StringComparison.OrdinalIgnoreCase)) return true;
            // Lights
            switch (nodeType)
            {
                case "ambientLight":
                case "directionalLight":
                case "pointLight":
                case "spotLight":
                case "areaLight":
                case "volumeLight":
                    return true;
            }
            return false;
        }

        public static bool IsShapeOrGeometryNodeType(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType)) return false;
            nodeType = nodeType.Trim();
            if (string.Equals(nodeType, "mesh", StringComparison.OrdinalIgnoreCase)) return true;
            if (nodeType.EndsWith("Shape", StringComparison.OrdinalIgnoreCase)) return true;
            // Common geometry families
            if (nodeType.IndexOf("nurbs", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("curve", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("surface", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("poly", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (nodeType.IndexOf("subdiv", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static bool ShouldCreateVisibleGameObject(string nodeType)
        {
            return IsDagObjectNodeType(nodeType) || IsCameraOrLightNodeType(nodeType) || IsShapeOrGeometryNodeType(nodeType);
        }
    }
}
