// File: Assets/MayaImporter/CheckerNode.cs
using UnityEngine;
using MayaImporter.Core;
using MayaImporter.Shader;

namespace MayaImporter.Shading
{
    [DisallowMultipleComponent]
    [MayaNodeType("checker")]
    public sealed class CheckerNode : MayaProceduralTextureNodeBase
    {
        [Header("Checker Params (best-effort)")]
        public Color color1 = Color.white;
        public Color color2 = Color.black;

        public int cellsU = 8;
        public int cellsV = 8;

        // ★ public override に統一
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            color1 = ReadColor3(new[] { ".color1", "color1", ".c1", "c1" }, color1);
            color2 = ReadColor3(new[] { ".color2", "color2", ".c2", "c2" }, color2);

            cellsU = Mathf.Clamp(ReadInt(new[] { ".cellsU", "cellsU" }, cellsU), 1, 256);
            cellsV = Mathf.Clamp(ReadInt(new[] { ".cellsV", "cellsV" }, cellsV), 1, 256);

            BakeToTextureMeta(log,
                bakeFunc: () =>
                {
                    var texMeta = EnsureTexMeta();
                    return MayaProceduralTextureBaker.BakeChecker(
                        Mathf.Max(2, bakeWidth), Mathf.Max(2, bakeHeight),
                        color1, color2,
                        texMeta.repeatUV, texMeta.offsetUV, texMeta.rotateUVDegrees,
                        cellsU, cellsV);
                },
                bakeLabel: "checker");

            log.Info($"[checker] c1={color1} c2={color2} cells=({cellsU},{cellsV})");
        }
    }
}
