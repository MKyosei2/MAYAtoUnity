using System;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Scene-wide settings captured at import-time.
    /// Maya / Autodesk API無しで、再生・評価に必要な最低限の情報だけ保持する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaSceneSettings : MonoBehaviour
    {
        [Header("Source")]
        public string sourcePath;

        [Header("Units")]
        public string timeUnit = "film"; // Maya currentUnit -t
        public double framesPerSecond = 24.0;

        [Header("Conversion")]
        public CoordinateConversion conversion = CoordinateConversion.MayaToUnity_MirrorZ;

        [Header("Playback (best-effort)")]
        public bool loop = true;

        /// <summary>
        /// Importer (UnitySceneBuilder) から初期化される想定。
        /// </summary>
        public void InitializeFrom(MayaSceneData scene, MayaImportOptions options)
        {
            sourcePath = scene?.SourcePath;

            conversion = options != null ? options.Conversion : CoordinateConversion.MayaToUnity_MirrorZ;

            // currentUnit -t
            string tu = null;
            if (scene != null && scene.SceneUnits != null)
            {
                scene.SceneUnits.TryGetValue("time", out tu);
            }
            if (!string.IsNullOrEmpty(tu))
                timeUnit = tu;

            framesPerSecond = MayaTimeUnitUtil.ResolveFramesPerSecond(timeUnit, defaultFps: 24.0);
        }
    }
}
