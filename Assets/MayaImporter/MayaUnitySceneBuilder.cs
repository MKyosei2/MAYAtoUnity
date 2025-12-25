// Assets/MayaImporter/MayaUnitySceneBuilder.cs
using System;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter
{
    /// <summary>
    /// MayaSceneData -> Unity GameObjects の入口を1箇所に固定するラッパ。
    /// </summary>
    public static class MayaUnitySceneBuilder
    {
        public static bool TryBuild(MayaSceneData scene, MayaImportOptions options, MayaImportLog log, out GameObject root)
        {
            root = null;

            if (scene == null)
                return false;

            try
            {
                var builder = new UnitySceneBuilderV2(options, log);
                root = builder.Build(scene);
                return root != null;
            }
            catch (Exception ex)
            {
                Debug.LogError("[MayaImporter] MayaUnitySceneBuilder.TryBuild failed:\n" + ex);
                return false;
            }
        }
    }
}
