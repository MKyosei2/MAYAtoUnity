using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-1 Step-3:
    /// Provide scene-wide context during ApplyToUnity() without rewriting every signature.
    /// </summary>
    public static class MayaBuildContext
    {
        public static MayaSceneData CurrentScene { get; private set; }
        public static MayaImportOptions CurrentOptions { get; private set; }
        public static MayaImportLog CurrentLog { get; private set; }

        // Cache materials so multiple meshes sharing one shadingEngine reuse the same Unity Material.
        public static readonly Dictionary<string, Material> MaterialByShadingEngine =
            new Dictionary<string, Material>(System.StringComparer.Ordinal);

        public static void Push(MayaSceneData scene, MayaImportOptions options, MayaImportLog log)
        {
            CurrentScene = scene;
            CurrentOptions = options;
            CurrentLog = log;
            MaterialByShadingEngine.Clear();
        }

        public static void Pop()
        {
            CurrentScene = null;
            CurrentOptions = null;
            CurrentLog = null;
            MaterialByShadingEngine.Clear();
        }
    }
}
