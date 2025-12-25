// NodeType: cacheFile (Phase C: non-empty decode)

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("cacheFile")]
    public sealed class MayaGenerated_CacheFileNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (cacheFile)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private string cachePath;
        [SerializeField] private string cacheName;
        [SerializeField] private float startTime;
        [SerializeField] private float endTime;
        [SerializeField] private float timeScale = 1f;

        [SerializeField] private string incomingTime;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            cachePath = ReadString("", ".cachePath", "cachePath", ".directory", "directory", ".path", "path", ".fileName", "fileName", ".filename", "filename");
            cacheName = ReadString("", ".cacheName", "cacheName", ".name", "name");
            startTime = ReadFloat(0f, ".startTime", "startTime", ".st", "st", ".startFrame", "startFrame");
            endTime = ReadFloat(0f, ".endTime", "endTime", ".et", "et", ".endFrame", "endFrame");
            timeScale = ReadFloat(1f, ".timeScale", "timeScale", ".ts", "ts");

            incomingTime = FindLastIncomingTo("time", "t");

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, path='{cachePath}', name='{cacheName}', start={startTime}, end={endTime}, timeScale={timeScale}, incomingTime={(string.IsNullOrEmpty(incomingTime) ? "none" : incomingTime)}");
        }
    }
}
