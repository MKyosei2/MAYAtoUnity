// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Production Step-3:
    /// Provide scene-wide context during ApplyToUnity() without rewriting every signature.
    /// </summary>
    public static class MayaBuildContext
    {
        public static MayaSceneData CurrentScene { get; private set; }
        public static MayaImportOptions CurrentOptions { get; private set; }
        public static MayaImportLog CurrentLog { get; private set; }

        public static GameObject CurrentRootObject { get; private set; }
        public static GameObject CurrentGraphRoot { get; private set; }

        // NodeName -> host GameObject (may be hidden for DG nodes)
        public static readonly Dictionary<string, GameObject> GameObjectByNodeName =
            new Dictionary<string, GameObject>(System.StringComparer.Ordinal);

        // Cache materials so multiple meshes sharing one shadingEngine reuse the same Unity Material.
        public static readonly Dictionary<string, Material> MaterialByShadingEngine =
            new Dictionary<string, Material>(System.StringComparer.Ordinal);

        public static void Push(MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            CurrentScene = scene;
            CurrentOptions = options;
            CurrentLog = log;
            MaterialByShadingEngine.Clear();
            // Keep Unity object mapping unless explicitly overwritten

        }

        public static void RegisterUnityObjects(GameObject rootObject, GameObject graphRoot, Dictionary<string, GameObject> mapping)
        {
            CurrentRootObject = rootObject;
            CurrentGraphRoot = graphRoot;
            GameObjectByNodeName.Clear();
            if (mapping != null)
            {
                foreach (var kv in mapping)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                    GameObjectByNodeName[kv.Key] = kv.Value;
                }
            }
        }

        public static void Pop()
        {
            CurrentScene = null;
            CurrentOptions = null;
            CurrentLog = null;
            CurrentRootObject = null;
            CurrentGraphRoot = null;
            GameObjectByNodeName.Clear();
            MaterialByShadingEngine.Clear();
            // Keep Unity object mapping unless explicitly overwritten

        }
    }
}
