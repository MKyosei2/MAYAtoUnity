// Auto-generated  Production implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicMeshMapper")]
    [DisallowMultipleComponent]
    public sealed class AlembicMeshMapper : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicMeshMapper)")]
        [SerializeField] private string objectPathInCache;
        [SerializeField] private string targetMesh;
        [SerializeField] private bool copyUVs = true;
        [SerializeField] private bool copyNormals = true;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            objectPathInCache = ReadString("",
                ".abcObjectPath", "abcObjectPath",
                ".objectPath", "objectPath",
                ".path", "path");

            targetMesh = ReadString("",
                ".targetMesh", "targetMesh",
                ".mesh", "mesh",
                ".target", "target");

            copyUVs = ReadBool(true, ".copyUVs", "copyUVs", ".uv", "uv");
            copyNormals = ReadBool(true, ".copyNormals", "copyNormals", ".nrm", "nrm");

            SetNotes(
                $"alembicMeshMapper decoded: objectPath='{objectPathInCache}', targetMesh='{targetMesh}', copyUVs={copyUVs}, copyNormals={copyNormals}"
            );
        }
    }
}
