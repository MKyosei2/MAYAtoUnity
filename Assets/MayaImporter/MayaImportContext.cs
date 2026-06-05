// MAYAIMPORTER_PERF: Shared import context for cached transform/component lookup during JSON runtime attachment
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Per-import cache for expensive hierarchy lookups.
    /// Build once after UnitySceneBuilder creates the hierarchy, then share it across mesh,
    /// skin, blendshape, material, camera, and light attachment stages.
    /// </summary>
    public sealed class MayaImportContext
    {
        private readonly Dictionary<Type, Dictionary<Transform, Component>> componentCaches =
            new Dictionary<Type, Dictionary<Transform, Component>>();

        public GameObject Root { get; private set; }
        public Transform RootTransform { get; private set; }
        public Dictionary<string, Transform> TransformIndex { get; private set; }

        private MayaImportContext(GameObject root)
        {
            Root = root;
            RootTransform = root != null ? root.transform : null;
            TransformIndex = new Dictionary<string, Transform>(StringComparer.Ordinal);
            RebuildTransformIndex();
        }

        public static MayaImportContext Build(GameObject root)
        {
            return new MayaImportContext(root);
        }

        public void RebuildTransformIndex()
        {
            TransformIndex.Clear();
            if (RootTransform == null) return;

            Transform[] transforms = Root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                string path = BuildUnityPathRelativeToRoot(RootTransform, t);
                AddTransformAlias(path, t);
                if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(Root.name))
                    AddTransformAlias(Root.name + "|" + path, t);
                AddTransformAlias(t.name, t);
            }

            AddTransformAlias(Root.name, RootTransform);
        }

        public Transform FindTransform(string path, string parentPath, string name, Transform fallback)
        {
            return FindTransformInIndex(TransformIndex, path, parentPath, name, fallback);
        }

        public T GetComponent<T>(Transform target) where T : Component
        {
            if (target == null) return null;

            Type type = typeof(T);
            Dictionary<Transform, Component> cache;
            if (!componentCaches.TryGetValue(type, out cache))
            {
                cache = new Dictionary<Transform, Component>();
                componentCaches.Add(type, cache);
            }

            Component component;
            if (cache.TryGetValue(target, out component))
                return component as T;

            T typed = target.GetComponent<T>();
            cache[target] = typed;
            return typed;
        }

        public T GetOrAddComponent<T>(Transform target) where T : Component
        {
            if (target == null) return null;

            T component = GetComponent<T>(target);
            if (component != null) return component;

            component = target.gameObject.AddComponent<T>();
            Type type = typeof(T);
            Dictionary<Transform, Component> cache;
            if (!componentCaches.TryGetValue(type, out cache))
            {
                cache = new Dictionary<Transform, Component>();
                componentCaches.Add(type, cache);
            }
            cache[target] = component;
            return component;
        }

        public void InvalidateComponent<T>(Transform target) where T : Component
        {
            if (target == null) return;
            Dictionary<Transform, Component> cache;
            if (componentCaches.TryGetValue(typeof(T), out cache))
                cache.Remove(target);
        }

        private void AddTransformAlias(string key, Transform transform)
        {
            if (transform == null || string.IsNullOrEmpty(key)) return;
            string stable = StableName(key, null);
            if (!string.IsNullOrEmpty(stable) && !TransformIndex.ContainsKey(stable))
                TransformIndex.Add(stable, transform);
        }

        public static Transform FindTransformInIndex(Dictionary<string, Transform> index, string path, string parentPath, string name, Transform fallback)
        {
            Transform t;
            string stable = StableName(path, name);
            if (!string.IsNullOrEmpty(stable) && index != null && index.TryGetValue(stable, out t)) return t;

            string parent = StableName(parentPath, null);
            if (!string.IsNullOrEmpty(parent) && index != null && index.TryGetValue(parent, out t)) return t;

            if (!string.IsNullOrEmpty(name) && index != null && index.TryGetValue(name, out t)) return t;
            return fallback;
        }

        public static string BuildUnityPathRelativeToRoot(Transform root, Transform target)
        {
            if (root == null || target == null) return string.Empty;
            var parts = new List<string>();
            Transform t = target;
            while (t != null && t != root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("|", parts.ToArray());
        }

        public static string StableName(string path, string name)
        {
            if (!string.IsNullOrEmpty(path)) return path.Trim().Trim('|');
            return string.IsNullOrEmpty(name) ? string.Empty : name.Trim();
        }
    }
}
