// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-6:
    /// Build a portfolio-grade verification report for a single imported scene.
    ///
    /// Guarantees:
    /// - Works in Unity-only environment.
    /// - Never throws (best-effort; errors are written to log/report).
    /// - Does not change reconstruction semantics; it only audits and records proof.
    /// </summary>
    public static class MayaPhase6Verification
    {
        private const int SampleNamesMax = 64;
        private const int TopTypesMax = 16;

        public static string BuildAndAttach(
            GameObject root,
            MayaSceneData scene,
            MayaImportOptions options,
            MayaReconstructionSelection selection,
            MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (root == null)
                return "[Phase6] root is null.";

            // Collect Unity-side node components
            var comps = root.GetComponentsInChildren<MayaNodeComponentBase>(true);
            var unityByName = new Dictionary<string, List<MayaNodeComponentBase>>(StringComparer.Ordinal);
            int unknownCount = 0;

            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                if (string.IsNullOrEmpty(c.NodeName)) continue;

                if (!unityByName.TryGetValue(c.NodeName, out var list))
                {
                    list = new List<MayaNodeComponentBase>(1);
                    unityByName.Add(c.NodeName, list);
                }
                list.Add(c);

                if (c is MayaUnknownNodeComponent)
                    unknownCount++;
            }

            // Scene-side nodes
            int sceneNodeCount = (scene != null && scene.Nodes != null) ? scene.Nodes.Count : 0;
            int sceneConnCount = (scene != null && scene.Connections != null) ? scene.Connections.Count : 0;

            // Coverage audit (if reconstruction disabled, we don't treat missing as failure)
            bool reconstructGO = (selection == null) ? true : selection.ReconstructGameObjects;

            var missingNames = new List<string>(256);
            var duplicateNames = new List<string>(256);

            // duplicates in Unity
            foreach (var kv in unityByName)
            {
                if (kv.Value != null && kv.Value.Count > 1)
                    duplicateNames.Add(kv.Key);
            }

            // missing nodes: scene has node but Unity lacks component
            if (scene != null && scene.Nodes != null)
            {
                foreach (var kv in scene.Nodes)
                {
                    var nodeName = kv.Key;
                    if (string.IsNullOrEmpty(nodeName)) continue;

                    if (!unityByName.ContainsKey(nodeName))
                    {
                        if (ShouldExpectInUnity(nodeName, kv.Value, selection, scene))
                            missingNames.Add(nodeName);
                    }
                }
            }

            // Unknown types histogram (Unity-side)
            var unknownTypeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (unknownCount > 0)
            {
                for (int i = 0; i < comps.Length; i++)
                {
                    var c = comps[i];
                    if (c == null) continue;
                    if (!(c is MayaUnknownNodeComponent)) continue;

                    var t = c.NodeType ?? "";
                    if (!unknownTypeCounts.TryGetValue(t, out var n)) n = 0;
                    unknownTypeCounts[t] = n + 1;
                }
            }

            // Build stable fingerprint (computed after Phase-7 marker scan)
            string fingerprint = null;

            // Attach fingerprint component
            var fp = root.GetComponent<MayaPhase6DeterminismFingerprint>();
            if (fp == null) fp = root.AddComponent<MayaPhase6DeterminismFingerprint>();

            fp.sourcePath = scene?.SourcePath ?? "";
            fp.sourceKind = (scene != null) ? scene.SourceKind.ToString() : "Unknown";
            fp.schemaVersion = (scene != null) ? scene.SchemaVersion : 0;

            fp.rawSha256 = scene?.RawSha256 ?? "";
            fp.fingerprintSha256 = "";

            fp.sceneNodeCount = sceneNodeCount;
            fp.sceneConnectionCount = sceneConnCount;

            fp.unityNodeComponentCount = comps != null ? comps.Length : 0;
            fp.unityUnknownNodeComponentCount = unknownCount;

            // .mb provenance counts (scene-side)
            ComputeMbProvenanceCounts(scene,
                out fp.mbEmbeddedAsciiNodeCount,
                out fp.mbNullTerminatedNodeCount,
                out fp.mbDeterministicNodeCount,
                out fp.mbChunkPlaceholderNodeCount,
                out fp.mbHeuristicNodeCount);


            fp.reconstructionGameObjectsEnabled = reconstructGO;
            fp.missingUnityNodeCount = reconstructGO ? missingNames.Count : 0;
            fp.duplicateUnityNodeNameCount = duplicateNames.Count;

            FillTopTypes(unknownTypeCounts, out fp.topUnknownNodeTypes, out fp.topUnknownNodeTypeCounts);

            // Phase-7: provisional marker histogram (best-effort features that still need "本実装").
            Dictionary<string, int> markerCounts = null;
            int markerTotal = 0;
            try
            {
                var markers = root.GetComponentsInChildren<MayaProvisionalMarker>(true);
                markerTotal = markers != null ? markers.Length : 0;
                if (markerTotal > 0)
                {
                    markerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < markers.Length; i++)
                    {
                        var m = markers[i];
                        if (m == null) continue;
                        var k = m.kind ?? "";
                        if (!markerCounts.TryGetValue(k, out var c)) c = 0;
                        markerCounts[k] = c + 1;
                    }
                }
            }
            catch
            {
                markerCounts = null;
                markerTotal = 0;
            }

            fp.provisionalMarkerCount = markerTotal;
            if (markerCounts != null && markerCounts.Count > 0)
                FillTopTypes(markerCounts, out fp.topProvisionalKinds, out fp.topProvisionalKindCounts);
            else
            {
                fp.topProvisionalKinds = Array.Empty<string>();
                fp.topProvisionalKindCounts = Array.Empty<int>();
            }

	            // Now compute a stable fingerprint that also includes the provisional marker histogram.
	            fingerprint = ComputeFingerprint(scene, options, selection, unknownTypeCounts, markerCounts);
	            fp.fingerprintSha256 = fingerprint ?? "";

            fp.sampleMissingNodeNames = TruncateNames(missingNames, SampleNamesMax);
            fp.sampleDuplicateNodeNames = TruncateNames(duplicateNames, SampleNamesMax);

            // Build report text (for TextAsset)
	            var report = BuildReportText(root, scene, options, selection, comps, unityByName, missingNames, duplicateNames, unknownTypeCounts, markerCounts, markerTotal, fingerprint);

            // emit warnings
            if (reconstructGO && missingNames.Count > 0)
                log.Warn($"[Phase6] Missing Unity node components: {missingNames.Count} / sceneNodes={sceneNodeCount} (see Phase6VerificationReport).");

            if (duplicateNames.Count > 0)
                log.Warn($"[Phase6] Duplicate Unity node names detected: {duplicateNames.Count} (see Phase6VerificationReport).");

            return report;
        }

        private static string[] TruncateNames(List<string> names, int max)
        {
            if (names == null || names.Count == 0) return Array.Empty<string>();
            int n = Math.Min(max, names.Count);
            var arr = new string[n];
            for (int i = 0; i < n; i++) arr[i] = names[i];
            return arr;
        }

        private static void FillTopTypes(Dictionary<string, int> typeCounts, out string[] types, out int[] counts)
        {
            types = Array.Empty<string>();
            counts = Array.Empty<int>();
            if (typeCounts == null || typeCounts.Count == 0) return;

            // select top N without LINQ
            var list = new List<KeyValuePair<string, int>>(typeCounts.Count);
            foreach (var kv in typeCounts) list.Add(kv);

            list.Sort((a, b) =>
            {
                int c = b.Value.CompareTo(a.Value);
                if (c != 0) return c;
                return StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key);
            });

            int n = Math.Min(TopTypesMax, list.Count);
            types = new string[n];
            counts = new int[n];

            for (int i = 0; i < n; i++)
            {
                types[i] = list[i].Key;
                counts[i] = list[i].Value;
            }
        }

        private static bool ShouldExpectInUnity(string nodeName, NodeRecord rec, MayaReconstructionSelection selection, MayaSceneData scene)
        {
            if (string.IsNullOrEmpty(nodeName) || rec == null) return false;

            // If selection is not enabled, default expects reconstruction to create a component for every node.
            if (selection == null || !selection.Enabled)
                return true;

            // Name-based exclusion
            if (selection.IsNodeNameExcluded(nodeName))
                return false;

            // Subtree exclusion (by parent chain)
            if (selection.TreatExcludedAsSubtree && scene != null)
            {
                var p = rec.ParentName;
                int guard = 0;
                while (!string.IsNullOrEmpty(p) && guard++ < 128)
                {
                    if (selection.IsNodeNameExcluded(p))
                        return false;

                    if (!scene.Nodes.TryGetValue(p, out var pr) || pr == null) break;
                    p = pr.ParentName;
                }
            }

            // Type-based exclusion
            if (selection.IsNodeTypeExcluded(rec.NodeType))
                return false;

            // Category toggles (best-effort; conservative lists)
            string t = rec.NodeType ?? "";

            if (!selection.ImportMeshes)
            {
                if (string.Equals(t, "mesh", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "nurbsCurve", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "nurbsSurface", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!selection.ImportMaterials)
            {
                if (t.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(t, "shadingEngine", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "lambert", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "blinn", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "phong", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!selection.ImportTextures)
            {
                if (string.Equals(t, "file", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "place2dTexture", StringComparison.OrdinalIgnoreCase) ||
                    t.IndexOf("texture", StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            if (!selection.ImportAnimationClip)
            {
                if (t.StartsWith("animCurve", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private static void ComputeMbProvenanceCounts(MayaSceneData scene,
            out int embeddedAscii, out int nullTerminated, out int deterministic, out int chunkPlaceholder, out int heuristic)
        {
            embeddedAscii = nullTerminated = deterministic = chunkPlaceholder = heuristic = 0;
            if (scene == null || scene.Nodes == null) return;

            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;

                switch (n.Provenance)
                {
                    case MayaNodeProvenance.MbEmbeddedAscii: embeddedAscii++; break;
                    case MayaNodeProvenance.MbNullTerminatedAscii: nullTerminated++; break;
                    case MayaNodeProvenance.MbDeterministicStringTable: deterministic++; break;
                    case MayaNodeProvenance.MbChunkPlaceholder: chunkPlaceholder++; break;
                    case MayaNodeProvenance.MbHeuristic: heuristic++; break;
                }
            }
        }

static string BuildReportText(
            GameObject root,
            MayaSceneData scene,
            MayaImportOptions options,
            MayaReconstructionSelection selection,
            MayaNodeComponentBase[] comps,
            Dictionary<string, List<MayaNodeComponentBase>> unityByName,
            List<string> missingNames,
            List<string> duplicateNames,
            Dictionary<string, int> unknownTypeCounts,
	            Dictionary<string, int> provisionalMarkerCounts,
	            int provisionalMarkerTotal,
            string fingerprintSha256)
        {
            var sb = new StringBuilder(16_384);
            sb.AppendLine("=== MayaImporter Phase6 Verification Report ===");
            sb.AppendLine($"GeneratedAt(UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            sb.AppendLine();

            sb.AppendLine("[Source]");
            sb.AppendLine($"  sourcePath: {scene?.SourcePath ?? ""}");
            sb.AppendLine($"  sourceKind: {(scene != null ? scene.SourceKind.ToString() : "Unknown")}");
            sb.AppendLine($"  schemaVersion: {(scene != null ? scene.SchemaVersion.ToString(CultureInfo.InvariantCulture) : "0")}");
            sb.AppendLine($"  rawSha256: {scene?.RawSha256 ?? ""}");
            sb.AppendLine($"  fingerprintSha256: {fingerprintSha256}");
            sb.AppendLine();

            sb.AppendLine("[Counts]");
            sb.AppendLine($"  sceneNodes: {(scene != null && scene.Nodes != null ? scene.Nodes.Count : 0)}");
            sb.AppendLine($"  sceneConnections: {(scene != null && scene.Connections != null ? scene.Connections.Count : 0)}");
            sb.AppendLine($"  unityNodeComponents: {(comps != null ? comps.Length : 0)}");
            sb.AppendLine($"  unityUnknownNodeComponents: {CountUnknown(comps)}");
            sb.AppendLine();

            sb.AppendLine("[Options]");
            sb.AppendLine($"  conversion: {options.Conversion}");
            sb.AppendLine($"  keepRawStatements: {options.KeepRawStatements}");
            sb.AppendLine($"  saveMeshes: {options.SaveMeshes}");
            sb.AppendLine($"  saveMaterials: {options.SaveMaterials}");
            sb.AppendLine($"  saveTextures: {options.SaveTextures}");
            sb.AppendLine($"  saveAnimationClip: {options.SaveAnimationClip}");
            sb.AppendLine();

            sb.AppendLine("[ReconstructionSelection]");
            if (selection == null)
            {
                sb.AppendLine("  selection: (null / disabled)");
                sb.AppendLine("  reconstructGameObjects: true");
            }
            else
            {
                sb.AppendLine($"  enabled: {selection.Enabled}");
                sb.AppendLine($"  reconstructGameObjects: {selection.ReconstructGameObjects}");
                sb.AppendLine($"  importMeshes: {selection.ImportMeshes}");
                sb.AppendLine($"  importMaterials: {selection.ImportMaterials}");
                sb.AppendLine($"  importTextures: {selection.ImportTextures}");
                sb.AppendLine($"  importAnimationClip: {selection.ImportAnimationClip}");
                sb.AppendLine($"  excludeByNameEnabled: {selection.ExcludeByNameEnabled}");
                sb.AppendLine($"  treatExcludedAsSubtree: {selection.TreatExcludedAsSubtree}");
                sb.AppendLine($"  excludeByNodeTypeEnabled: {selection.ExcludeByNodeTypeEnabled}");
            }
            sb.AppendLine();

            sb.AppendLine("[Coverage]");
            bool reconstruct = (selection == null) ? true : selection.ReconstructGameObjects;
            if (!reconstruct)
            {
                sb.AppendLine("  NOTE: reconstructGameObjects is OFF, so missing Unity nodes are expected.");
            }
            sb.AppendLine($"  missingUnityNodes: {(reconstruct ? missingNames.Count : 0)}");
            sb.AppendLine($"  duplicateUnityNodeNames: {duplicateNames.Count}");
            sb.AppendLine();

            if (reconstruct && missingNames.Count > 0)
            {
                sb.AppendLine("  -- sample missing node names --");
                WriteNameList(sb, missingNames, SampleNamesMax);
                sb.AppendLine();
            }

            if (duplicateNames.Count > 0)
            {
                sb.AppendLine("  -- sample duplicate node names --");
                WriteNameList(sb, duplicateNames, SampleNamesMax);
                sb.AppendLine();
            }

            sb.AppendLine("[Unknown NodeTypes]");
            if (unknownTypeCounts == null || unknownTypeCounts.Count == 0)
            {
                sb.AppendLine("  (none)");
            }
            else
            {
                var top = new List<KeyValuePair<string, int>>(unknownTypeCounts.Count);
                foreach (var kv in unknownTypeCounts) top.Add(kv);

                top.Sort((a, b) =>
                {
                    int c = b.Value.CompareTo(a.Value);
                    if (c != 0) return c;
                    return StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key);
                });

                int n = Math.Min(TopTypesMax, top.Count);
                for (int i = 0; i < n; i++)
                    sb.AppendLine($"  {top[i].Key}: {top[i].Value}");

                if (top.Count > n)
                    sb.AppendLine($"  ...(and {top.Count - n} more)");
            }

	            sb.AppendLine();
	            sb.AppendLine("[Provisional Markers]");
	            sb.AppendLine($"  total: {provisionalMarkerTotal}");
	            if (provisionalMarkerCounts == null || provisionalMarkerCounts.Count == 0)
	            {
	                sb.AppendLine("  (none)");
	            }
	            else
	            {
	                var top = new List<KeyValuePair<string, int>>(provisionalMarkerCounts.Count);
	                foreach (var kv in provisionalMarkerCounts) top.Add(kv);

	                top.Sort((a, b) =>
	                {
	                    int c = b.Value.CompareTo(a.Value);
	                    if (c != 0) return c;
	                    return StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key);
	                });

	                int n = Math.Min(TopTypesMax, top.Count);
	                for (int i = 0; i < n; i++)
	                    sb.AppendLine($"  {top[i].Key}: {top[i].Value}");

	                if (top.Count > n)
	                    sb.AppendLine($"  ...(and {top.Count - n} more)");
	            }

            sb.AppendLine();
            sb.AppendLine("[Notes]");
            sb.AppendLine("  - \"100%\" (lossless hold) is proven by rawSha256 + preserved SceneData/Statements.");
            sb.AppendLine("  - \"100点\" (reconstructability) is proven by per-node Unity component presence and runtime placeholder components.");
            sb.AppendLine("  - If missingUnityNodes > 0 while reconstructGameObjects is ON, that indicates a bug/regression.");
            sb.AppendLine();

            return sb.ToString();
        }

        private static void WriteNameList(StringBuilder sb, List<string> names, int max)
        {
            int n = Math.Min(max, names.Count);
            for (int i = 0; i < n; i++)
                sb.AppendLine("    " + names[i]);

            if (names.Count > n)
                sb.AppendLine($"    ...(and {names.Count - n} more)");
        }

        private static int CountUnknown(MayaNodeComponentBase[] comps)
        {
            if (comps == null) return 0;
            int n = 0;
            for (int i = 0; i < comps.Length; i++)
                if (comps[i] is MayaUnknownNodeComponent) n++;
            return n;
        }

        private static string ComputeFingerprint(
            MayaSceneData scene,
            MayaImportOptions options,
            MayaReconstructionSelection selection,
	            Dictionary<string, int> unknownTypeCounts,
	            Dictionary<string, int> provisionalMarkerCounts)
        {
            // Stable by design:
            // - RawSha256 already fingerprints file content.
            // - Add schema + options + selection + nodeType histogram (sorted) for deterministic import state.
            var sb = new StringBuilder(8192);

            sb.Append("raw=").Append(scene?.RawSha256 ?? "").Append('|');
            sb.Append("schema=").Append(scene != null ? scene.SchemaVersion.ToString(CultureInfo.InvariantCulture) : "0").Append('|');
            sb.Append("kind=").Append(scene != null ? scene.SourceKind.ToString() : "Unknown").Append('|');

            sb.Append("conv=").Append(options != null ? options.Conversion.ToString() : "null").Append('|');
            sb.Append("keepRaw=").Append(options != null && options.KeepRawStatements ? "1" : "0").Append('|');

            if (selection != null)
            {
                sb.Append("sel=1").Append('|');
                sb.Append("reconGO=").Append(selection.ReconstructGameObjects ? "1" : "0").Append('|');
                sb.Append("m=").Append(selection.ImportMeshes ? "1" : "0").Append('|');
                sb.Append("mat=").Append(selection.ImportMaterials ? "1" : "0").Append('|');
                sb.Append("tex=").Append(selection.ImportTextures ? "1" : "0").Append('|');
                sb.Append("anim=").Append(selection.ImportAnimationClip ? "1" : "0").Append('|');
            }
            else
            {
                sb.Append("sel=0|");
            }

            int nodeCount = (scene != null && scene.Nodes != null) ? scene.Nodes.Count : 0;
            int connCount = (scene != null && scene.Connections != null) ? scene.Connections.Count : 0;
            sb.Append("nodes=").Append(nodeCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append("conns=").Append(connCount.ToString(CultureInfo.InvariantCulture)).Append('|');

            // Unknown types histogram also stable (sorted)
            if (unknownTypeCounts != null && unknownTypeCounts.Count > 0)
            {
                var list = new List<KeyValuePair<string, int>>(unknownTypeCounts.Count);
                foreach (var kv in unknownTypeCounts) list.Add(kv);

                list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));

                sb.Append("unknownTypes=");
                for (int i = 0; i < list.Count; i++)
                {
                    sb.Append(list[i].Key).Append(':').Append(list[i].Value.ToString(CultureInfo.InvariantCulture)).Append(',');
                }
                sb.Append('|');
            }

	            // Provisional marker histogram also stable (sorted)
	            if (provisionalMarkerCounts != null && provisionalMarkerCounts.Count > 0)
	            {
	                var list = new List<KeyValuePair<string, int>>(provisionalMarkerCounts.Count);
	                foreach (var kv in provisionalMarkerCounts) list.Add(kv);

	                list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));

	                sb.Append("provisional=");
	                for (int i = 0; i < list.Count; i++)
	                {
	                    sb.Append(list[i].Key).Append(':').Append(list[i].Value.ToString(CultureInfo.InvariantCulture)).Append(',');
	                }
	                sb.Append('|');
	            }

	            // Provisional markers histogram also stable (sorted)
	            if (provisionalMarkerCounts != null && provisionalMarkerCounts.Count > 0)
	            {
	                var list = new List<KeyValuePair<string, int>>(provisionalMarkerCounts.Count);
	                foreach (var kv in provisionalMarkerCounts) list.Add(kv);

	                list.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key));

	                sb.Append("prov=");
	                for (int i = 0; i < list.Count; i++)
	                {
	                    sb.Append(list[i].Key).Append(':').Append(list[i].Value.ToString(CultureInfo.InvariantCulture)).Append(',');
	                }
	                sb.Append('|');
	            }

            return Sha256Hex(sb.ToString());
        }

        private static string Sha256Hex(string text)
        {
            if (text == null) text = "";
            var bytes = Encoding.UTF8.GetBytes(text);

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }
}
