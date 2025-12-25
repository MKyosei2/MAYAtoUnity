// File: Assets/MayaImporter/GammaCorrectNode.cs
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("gammaCorrect")]
    public sealed class GammaCorrectNode : MayaProceduralTextureNodeBase
    {
        public float gamma = 2.2f;

        // ★ public override に統一
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            gamma = ReadFloat(new[] { ".gamma", "gamma", ".g", "g", ".value", "value" }, gamma);
            gamma = Mathf.Clamp(gamma, 0.0001f, 16f);

            BakeToTextureMeta(log,
                bakeFunc: () =>
                {
                    if (TryLoadSourceTextureFromNodeName(connectedInputTextureNodeName, out var src, out _))
                        return MayaProceduralTextureBaker.BakeGamma(src, gamma);

                    var texMeta = EnsureTexMeta();
                    var flat = MayaProceduralTextureBaker.BakeChecker(
                        Mathf.Max(2, bakeWidth), Mathf.Max(2, bakeHeight),
                        Color.gray, Color.gray,
                        texMeta.repeatUV, texMeta.offsetUV, texMeta.rotateUVDegrees,
                        1, 1);
                    return MayaProceduralTextureBaker.BakeGamma(flat, gamma);
                },
                bakeLabel: "gammaCorrect");

            log.Info($"[gammaCorrect] gamma={gamma} input={connectedInputTextureNodeName}");
        }
    }
}
