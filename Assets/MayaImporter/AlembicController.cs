// Auto-generated  Production implementation (decoded, non-empty).
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Alembic
{
    [MayaNodeType("alembicController")]
    [DisallowMultipleComponent]
    public sealed class AlembicController : MayaPhaseCNodeBase
    {
        [Header("Decoded (alembicController)")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private string abcFilePath;

        [SerializeField] private float timeOffset;
        [SerializeField] private float timeScale = 1f;

        [SerializeField] private string incomingTimePlug;
        [SerializeField] private string incomingCacheTimePlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            // File path keys vary a lot in user scenes / exporter versions
            abcFilePath = ReadString("",
                ".abcFile", "abcFile",
                ".fileName", "fileName",
                ".cacheFileName", "cacheFileName",
                ".path", "path");

            // Enabled flags: treat "mute/disabled" as negative; fall back to explicit enable/enabled.
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            timeOffset = ReadFloat(0f, ".timeOffset", "timeOffset", ".to", "to");
            timeScale = ReadFloat(1f, ".timeScale", "timeScale", ".ts", "ts");

            incomingTimePlug = FindLastIncomingTo("time", "t");
            incomingCacheTimePlug = FindLastIncomingTo("cacheTime", "ct", "cache");

            SetNotes(
                $"alembicController decoded: enabled={enabled}, file='{abcFilePath}', timeScale={timeScale}, timeOffset={timeOffset}, " +
                $"incomingTime={(string.IsNullOrEmpty(incomingTimePlug) ? "none" : incomingTimePlug)}, " +
                $"incomingCacheTime={(string.IsNullOrEmpty(incomingCacheTimePlug) ? "none" : incomingCacheTimePlug)}"
            );
        }
    }
}
