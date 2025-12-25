// Auto-generated placeholder -> Phase C implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicCacheTime")]
    [DisallowMultipleComponent]
    public sealed class AlembicCacheTimeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicCacheTime)")]
        [SerializeField] private float time;
        [SerializeField] private float startTime;
        [SerializeField] private float endTime;

        [SerializeField] private string incomingTimePlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // Typical keys (best-effort aliases)
            time = ReadFloat(0f, ".time", "time", ".t", "t");
            startTime = ReadFloat(0f, ".startTime", "startTime", ".startFrame", "startFrame", ".st", "st");
            endTime = ReadFloat(0f, ".endTime", "endTime", ".endFrame", "endFrame", ".et", "et");

            incomingTimePlug = FindLastIncomingTo("time", "t");

            SetNotes(
                $"alembicCacheTime decoded: time={time}, start={startTime}, end={endTime}, " +
                $"incomingTime={(string.IsNullOrEmpty(incomingTimePlug) ? "none" : incomingTimePlug)}"
            );
        }
    }
}
