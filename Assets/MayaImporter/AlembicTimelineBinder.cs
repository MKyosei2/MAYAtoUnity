// Auto-generated  Production implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicTimelineBinder")]
    [DisallowMultipleComponent]
    public sealed class AlembicTimelineBinder : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicTimelineBinder)")]
        [SerializeField] private bool useSceneTime = true;
        [SerializeField] private float timeScale = 1f;
        [SerializeField] private float timeOffset;

        [SerializeField] private string incomingTimePlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            useSceneTime = ReadBool(true, ".useSceneTime", "useSceneTime", ".ust", "ust");
            timeScale = ReadFloat(1f, ".timeScale", "timeScale", ".ts", "ts");
            timeOffset = ReadFloat(0f, ".timeOffset", "timeOffset", ".to", "to");

            incomingTimePlug = FindLastIncomingTo("time", "t");

            SetNotes(
                $"alembicTimelineBinder decoded: useSceneTime={useSceneTime}, timeScale={timeScale}, timeOffset={timeOffset}, " +
                $"incomingTime={(string.IsNullOrEmpty(incomingTimePlug) ? "none" : incomingTimePlug)}"
            );
        }
    }
}
