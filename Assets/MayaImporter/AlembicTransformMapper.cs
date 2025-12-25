// Auto-generated placeholder -> Phase C implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicTransformMapper")]
    [DisallowMultipleComponent]
    public sealed class AlembicTransformMapper : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicTransformMapper)")]
        [SerializeField] private string objectPathInCache;
        [SerializeField] private string targetTransform;

        [SerializeField] private bool applyRotation = true;
        [SerializeField] private bool applyTranslation = true;
        [SerializeField] private bool applyScale = true;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            objectPathInCache = ReadString("",
                ".abcObjectPath", "abcObjectPath",
                ".objectPath", "objectPath");

            targetTransform = ReadString("",
                ".targetTransform", "targetTransform",
                ".tr", "tr",
                ".target", "target");

            applyRotation = ReadBool(true, ".applyRotation", "applyRotation", ".ar", "ar");
            applyTranslation = ReadBool(true, ".applyTranslation", "applyTranslation", ".at", "at");
            applyScale = ReadBool(true, ".applyScale", "applyScale", ".as", "as");

            SetNotes(
                $"alembicTransformMapper decoded: objectPath='{objectPathInCache}', targetTransform='{targetTransform}', " +
                $"T={applyTranslation}, R={applyRotation}, S={applyScale}"
            );
        }
    }
}
