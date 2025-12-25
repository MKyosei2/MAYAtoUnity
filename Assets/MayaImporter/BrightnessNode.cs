// File: Assets/MayaImporter/BrightnessNode.cs
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("brightness")]
    public sealed class BrightnessNode : MayaProceduralTextureNodeBase
    {
        public float brightness = 0.0f;

        // ★ public override に統一
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            brightness = ReadFloat(new[] { ".brightness", "brightness" }, brightness);
            brightness = Mathf.Clamp(brightness, -1f, 1f);

            BakeToTextureMeta(log,
                bakeFunc: () =>
                {
                    if (TryLoadSourceTextureFromNodeName(connectedInputTextureNodeName, out var src, out _))
                        return MayaProceduralTextureBaker.BakeBrightnessContrast(src, brightness: brightness, contrast: 1f);

                    var texMeta = EnsureTexMeta();
                    return MayaProceduralTextureBaker.BakeChecker(
                        Mathf.Max(2, bakeWidth), Mathf.Max(2, bakeHeight),
                        Color.gray, Color.gray,
                        texMeta.repeatUV, texMeta.offsetUV, texMeta.rotateUVDegrees,
                        1, 1);
                },
                bakeLabel: "brightness");

            log.Info($"[brightness] brightness={brightness} input={connectedInputTextureNodeName}");
        }
    }
}
