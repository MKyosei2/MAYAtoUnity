using System;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase2 Final: single post-process entry.
    /// Parse後に必ず1回だけ呼ぶ。
    /// </summary>
    public static class MayaScenePostProcessor
    {
        public static void Apply(MayaSceneData scene, MayaImportLog log)
        {
            if (scene == null) return;

            var path = scene.SourcePath ?? "";
            var isMa = path.EndsWith(".ma", StringComparison.OrdinalIgnoreCase);
            var isMb = path.EndsWith(".mb", StringComparison.OrdinalIgnoreCase);

            // .ma
            if (isMa)
            {
                // 既にMayaAsciiParser内で呼んでいても二重でも害は少ないが、
                // ここが正になるので Parser 側からは呼ばない運用に寄せる。
                try { MayaMaShadingTaggerV2.Apply(scene, log); }
                catch (Exception ex) { log?.Warn($".ma postprocess: tagger failed: {ex.Message}"); }
            }

            // .mb
            if (isMb)
            {
                try { MayaMbShadingTagger.Apply(scene, log); }
                catch (Exception ex) { log?.Warn($".mb postprocess: shading tagger failed: {ex.Message}"); }
            }
        }
    }
}
