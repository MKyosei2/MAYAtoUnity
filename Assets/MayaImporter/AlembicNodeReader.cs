// Auto-generated placeholder -> Phase C implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicNodeReader")]
    [DisallowMultipleComponent]
    public sealed class AlembicNodeReader : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicNodeReader)")]
        [SerializeField] private string abcFilePath;
        [SerializeField] private string objectPathInCache;

        [SerializeField] private bool readTransforms = true;
        [SerializeField] private bool readMeshes = true;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            abcFilePath = ReadString("",
                ".abcFile", "abcFile",
                ".fileName", "fileName",
                ".cacheFileName", "cacheFileName",
                ".path", "path");

            objectPathInCache = ReadString("",
                ".abcObjectPath", "abcObjectPath",
                ".objectPath", "objectPath");

            readTransforms = ReadBool(true, ".readTransforms", "readTransforms", ".rt", "rt");
            readMeshes = ReadBool(true, ".readMeshes", "readMeshes", ".rm", "rm");

            SetNotes(
                $"alembicNodeReader decoded: file='{abcFilePath}', objectPath='{objectPathInCache}', readTransforms={readTransforms}, readMeshes={readMeshes}"
            );
        }
    }
}
