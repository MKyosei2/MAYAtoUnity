// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-6:
    /// Deterministic fingerprint + verification summary for portfolio proof.
    ///
    /// - Does NOT require Maya/Autodesk API.
    /// - Attached to imported root at import time.
    /// - Stores a stable SHA256 fingerprint derived from preserved data (raw hash + node graph summary)
    ///   and verification results (missing/duplicate/unknown counts).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaPhase6DeterminismFingerprint : MonoBehaviour
    {
        [Header("Source")]
        public string sourcePath;
        public string sourceKind;
        public int schemaVersion;

        [Header("Stable hashes")]
        public string rawSha256;
        public string fingerprintSha256;

        [Header("Counts")]
        public int sceneNodeCount;
        public int sceneConnectionCount;

        public int unityNodeComponentCount;
        public int unityUnknownNodeComponentCount;

        [Header(".mb provenance counts (scene-side)")]
        public int mbEmbeddedAsciiNodeCount;
        public int mbNullTerminatedNodeCount;
        public int mbDeterministicNodeCount;
        public int mbChunkPlaceholderNodeCount;
        public int mbHeuristicNodeCount;


        [Header("Coverage results")]
        public bool reconstructionGameObjectsEnabled;
        public int missingUnityNodeCount;
        public int duplicateUnityNodeNameCount;

        [Header("Phase7: Provisional markers")]
        public int provisionalMarkerCount;
        public string[] topProvisionalKinds = Array.Empty<string>();
        public int[] topProvisionalKindCounts = Array.Empty<int>();

        [Header("Top summaries (truncated)")]
        public string[] topUnknownNodeTypes = Array.Empty<string>();
        public int[] topUnknownNodeTypeCounts = Array.Empty<int>();

        public string[] sampleMissingNodeNames = Array.Empty<string>();
        public string[] sampleDuplicateNodeNames = Array.Empty<string>();
    }
}
