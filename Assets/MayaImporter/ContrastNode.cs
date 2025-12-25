// File: Assets/MayaImporter/ContrastNode.cs
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("contrast")]
    public sealed class ContrastNode : MayaProceduralTextureNodeBase
    {
        public float contrast = 1.0f;

        // ★ public override に統一
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            contrast = ReadFloat(new[] { ".contrast", "contrast" }, contrast);
            contrast = Mathf.Clamp(contrast, 0f, 8f);

            BakeToTextureMeta(log,
                bakeFunc: () =>
                {
                    if (TryLoadSourceTextureFromNodeName(connectedInputTextureNodeName, out var src, out _))
                        return MayaProceduralTextureBaker.BakeBrightnessContrast(src, brightness: 0f, contrast: contrast);

                    var texMeta = EnsureTexMeta();
                    return MayaProceduralTextureBaker.BakeChecker(
                        Mathf.Max(2, bakeWidth), Mathf.Max(2, bakeHeight),
                        Color.gray, Color.gray,
                        texMeta.repeatUV, texMeta.offsetUV, texMeta.rotateUVDegrees,
                        1, 1);
                },
                bakeLabel: "contrast");

            log.Info($"[contrast] contrast={contrast} input={connectedInputTextureNodeName}");
        }
    }
}
