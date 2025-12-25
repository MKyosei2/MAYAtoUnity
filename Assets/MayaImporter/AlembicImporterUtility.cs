// Auto-generated placeholder -> Phase C implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicImporterUtility")]
    [DisallowMultipleComponent]
    public sealed class AlembicImporterUtility : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicImporterUtility)")]
        [SerializeField] private string abcFilePath;
        [SerializeField] private bool flattenHierarchy;
        [SerializeField] private bool createMissingTransforms = true;
        [SerializeField] private float importScale = 1f;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            abcFilePath = ReadString("",
                ".abcFile", "abcFile",
                ".fileName", "fileName",
                ".cacheFileName", "cacheFileName",
                ".path", "path");

            flattenHierarchy = ReadBool(false, ".flattenHierarchy", "flattenHierarchy", ".fh", "fh");
            createMissingTransforms = ReadBool(true, ".createMissingTransforms", "createMissingTransforms", ".cmt", "cmt");
            importScale = ReadFloat(1f, ".importScale", "importScale", ".scale", "scale", ".s", "s");

            SetNotes(
                $"alembicImporterUtility decoded: file='{abcFilePath}', flattenHierarchy={flattenHierarchy}, " +
                $"createMissingTransforms={createMissingTransforms}, importScale={importScale}"
            );
        }
    }
}
