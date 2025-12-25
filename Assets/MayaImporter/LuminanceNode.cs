// File: Assets/MayaImporter/LuminanceNode.cs
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("luminance")]
    public sealed class LuminanceNode : MayaProceduralTextureNodeBase
    {
        // ★ public override に統一
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            BakeToTextureMeta(log,
                bakeFunc: () =>
                {
                    if (TryLoadSourceTextureFromNodeName(connectedInputTextureNodeName, out var src, out _))
                        return MayaProceduralTextureBaker.BakeLuminance(src);

                    var texMeta = EnsureTexMeta();
                    return MayaProceduralTextureBaker.BakeChecker(
                        Mathf.Max(2, bakeWidth), Mathf.Max(2, bakeHeight),
                        Color.gray, Color.gray,
                        texMeta.repeatUV, texMeta.offsetUV, texMeta.rotateUVDegrees,
                        1, 1);
                },
                bakeLabel: "luminance");

            log.Info($"[luminance] input={connectedInputTextureNodeName}");
        }
    }
}
