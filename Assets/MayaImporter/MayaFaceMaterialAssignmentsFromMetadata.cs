using System;
using System.Collections.Generic;
using System.Globalization;
using MayaImporter.Components;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Face-material (shadingEngine) assignment fallback using MayaShadingGroupMetadata.
    /// - Works even when MayaFaceMaterialAssignments cannot scan .ma raw statements (e.g. .mb import path).
    /// - Uses members recorded by ShadingEngineNode (whole-object and .f[...] components).
    /// - If a face is assigned multiple times, later processed shadingEngine wins (deterministic order by SG node name).
    /// </summary>
    public static class MayaFaceMaterialAssignmentsFromMetadata
    {
        /// <summary>
        /// Compatibility wrapper: older call-sites used this name.
        /// </summary>
        public static bool TryBuildAssignmentsFromScene(
            Transform sceneRoot,
            string meshNodeName,
            int faceCount,
            out MayaFaceMaterialAssignments.MeshAssignments result)
        {
            return TryGetForMesh(sceneRoot, meshNodeName, faceCount, out result);
        }

        public static bool TryGetForMesh(
            Transform sceneRoot,
            string meshNodeName,
            int faceCount,
            out MayaFaceMaterialAssignments.MeshAssignments result)
        {
            result = null;
            if (sceneRoot == null) return false;
            if (string.IsNullOrEmpty(meshNodeName)) return false;
            if (faceCount <= 0) return false;

            var meshLeaf = MayaPlugUtil.LeafName(meshNodeName);
            if (string.IsNullOrEmpty(meshLeaf)) return false;

            var metas = sceneRoot.GetComponentsInChildren<MayaShadingGroupMetadata>(true);
            if (metas == null || metas.Length == 0) return false;

            // Build (sgName, meta) list with deterministic order.
            var list = new List<(string sgName, MayaShadingGroupMetadata meta)>(metas.Length);
            for (int i = 0; i < metas.Length; i++)
            {
                var m = metas[i];
                if (m == null) continue;

                var sgName = TryGetShadingEngineNodeName(m);
                if (string.IsNullOrEmpty(sgName)) continue;

                list.Add((sgName, m));
            }

            if (list.Count == 0) return false;

            list.Sort((a, b) => string.CompareOrdinal(a.sgName, b.sgName));

            var outAssign = new MayaFaceMaterialAssignments.MeshAssignments();

            // Track winning SG per face to keep Unity submesh faces disjoint.
            var assignedBy = new int[faceCount];
            for (int i = 0; i < assignedBy.Length; i++) assignedBy[i] = -1;

            // Build sets in the same order as outAssign.FacesByShadingEngine insertion.
            for (int i = 0; i < list.Count; i++)
            {
                var sgName = list[i].sgName;
                var meta = list[i].meta;
                if (meta == null || meta.members == null || meta.members.Count == 0) continue;

                int sgIndex;
                var set = GetOrCreateWithIndex(outAssign, sgName, out sgIndex);

                // For each member
                for (int mi = 0; mi < meta.members.Count; mi++)
                {
                    var mem = meta.members[mi];
                    if (string.IsNullOrEmpty(mem.nodeName)) continue;

                    var leaf = MayaPlugUtil.LeafName(mem.nodeName);
                    if (!string.Equals(leaf, meshLeaf, StringComparison.Ordinal))
                        continue;

                    if (string.IsNullOrEmpty(mem.componentSpec))
                    {
                        // Whole-object membership -> all faces
                        AssignRange(set, assignedBy, sgIndex, outAssign, 0, faceCount - 1);
                        continue;
                    }

                    if (!TryParseFaceComponent(mem.componentSpec, out var inside))
                        continue;

                    AssignFromInside(set, assignedBy, sgIndex, outAssign, inside, faceCount);
                }
            }

            // Remove empty SGs to avoid zero-triangle submeshes.
            for (int i = outAssign.FacesByShadingEngine.Count - 1; i >= 0; i--)
            {
                var kv = outAssign.FacesByShadingEngine[i];
                if (kv.Value == null || kv.Value.Count == 0)
                    outAssign.FacesByShadingEngine.RemoveAt(i);
            }

            if (outAssign.FacesByShadingEngine.Count == 0)
                return false;

            result = outAssign;
            return true;
        }

        private static string TryGetShadingEngineNodeName(MayaShadingGroupMetadata meta)
        {
            if (meta == null) return null;

            // Prefer MayaNodeComponentBase.NodeName (full maya name/path).
            var node = meta.GetComponent<MayaNodeComponentBase>();
            if (node != null && string.Equals(node.NodeType, "shadingEngine", StringComparison.Ordinal))
                return node.NodeName;

            // Fallback: GameObject name (usually leaf).
            return meta.gameObject != null ? meta.gameObject.name : null;
        }

        private static HashSet<int> GetOrCreateWithIndex(
            MayaFaceMaterialAssignments.MeshAssignments ma,
            string shadingEngine,
            out int index)
        {
            for (int i = 0; i < ma.FacesByShadingEngine.Count; i++)
            {
                if (string.Equals(ma.FacesByShadingEngine[i].Key, shadingEngine, StringComparison.Ordinal))
                {
                    index = i;
                    return ma.FacesByShadingEngine[i].Value;
                }
            }

            var set = new HashSet<int>();
            ma.FacesByShadingEngine.Add(new KeyValuePair<string, HashSet<int>>(shadingEngine, set));
            index = ma.FacesByShadingEngine.Count - 1;
            return set;
        }

        private static bool TryParseFaceComponent(string componentSpec, out string inside)
        {
            inside = null;
            if (string.IsNullOrEmpty(componentSpec)) return false;

            // Accept ".f[...]" or "f[...]" (best-effort)
            var s = componentSpec.Trim();

            int f = s.IndexOf("f[", StringComparison.Ordinal);
            if (f < 0) return false;

            int lb = s.IndexOf('[', f);
            int rb = s.IndexOf(']', lb + 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            inside = s.Substring(lb + 1, rb - lb - 1).Trim();
            return !string.IsNullOrEmpty(inside);
        }

        private static void AssignFromInside(
            HashSet<int> set,
            int[] assignedBy,
            int sgIndex,
            MayaFaceMaterialAssignments.MeshAssignments ma,
            string inside,
            int faceCount)
        {
            inside = inside.Trim();
            if (string.IsNullOrEmpty(inside)) return;

            // patterns: "12" or "0:11" or "0:*" or "*" (and rarely "12:12")
            if (inside == "*")
            {
                AssignRange(set, assignedBy, sgIndex, ma, 0, faceCount - 1);
                return;
            }

            int colon = inside.IndexOf(':');
            if (colon < 0)
            {
                if (TryInt(inside, out var one))
                    AssignRange(set, assignedBy, sgIndex, ma, one, one);
                return;
            }

            var a = inside.Substring(0, colon).Trim();
            var b = inside.Substring(colon + 1).Trim();

            if (!TryInt(a, out var start))
                return;

            if (b == "*")
            {
                AssignRange(set, assignedBy, sgIndex, ma, start, faceCount - 1);
                return;
            }

            if (!TryInt(b, out var end))
                return;

            if (end < start) (start, end) = (end, start);
            AssignRange(set, assignedBy, sgIndex, ma, start, end);
        }

        private static void AssignRange(
            HashSet<int> set,
            int[] assignedBy,
            int sgIndex,
            MayaFaceMaterialAssignments.MeshAssignments ma,
            int start,
            int end)
        {
            if (start < 0) start = 0;
            if (end < start) end = start;

            if (end >= assignedBy.Length) end = assignedBy.Length - 1;

            for (int fi = start; fi <= end; fi++)
            {
                int prev = assignedBy[fi];
                if (prev >= 0 && prev < ma.FacesByShadingEngine.Count && prev != sgIndex)
                {
                    var prevSet = ma.FacesByShadingEngine[prev].Value;
                    if (prevSet != null) prevSet.Remove(fi);
                }

                set.Add(fi);
                assignedBy[fi] = sgIndex;
            }
        }

        private static bool TryInt(string s, out int v)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
    }
}