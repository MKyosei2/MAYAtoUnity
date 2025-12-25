// File: Assets/MayaImporter/NoiseNode.cs
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("noise")]
    public sealed class NoiseNode : MayaProceduralTextureNodeBase
    {
        public Color colorA = Color.black;
        public Color colorB = Color.white;
        public float frequency = 8f;
        public int seed = 0;

        // ★ public override に統一
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            colorA = ReadColor3(new[] { ".color1", "color1", ".c1", "c1", ".colorA", "colorA" }, colorA);
            colorB = ReadColor3(new[] { ".color2", "color2", ".c2", "c2", ".colorB", "colorB" }, colorB);

            frequency = ReadFloat(new[] { ".frequency", "frequency", ".freq", "freq", ".scale", "scale" }, frequency);
            frequency = Mathf.Clamp(frequency, 0.001f, 1024f);

            seed = ReadInt(new[] { ".seed", "seed" }, seed);

            BakeToTextureMeta(log,
                bakeFunc: () =>
                {
                    var texMeta = EnsureTexMeta();
                    return MayaProceduralTextureBaker.BakeNoise(
                        Mathf.Max(2, bakeWidth), Mathf.Max(2, bakeHeight),
                        colorA, colorB,
                        frequency,
                        texMeta.repeatUV, texMeta.offsetUV, texMeta.rotateUVDegrees,
                        seed);
                },
                bakeLabel: "noise");

            log.Info($"[noise] freq={frequency} seed={seed} A={colorA} B={colorB}");
        }
    }
}
