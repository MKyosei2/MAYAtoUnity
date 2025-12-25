// Assets/MayaImporter/DeformerBase.cs
//
// Phase-2 (Deformer common):
// - Provide a real ApplyToUnity implementation (no longer classified as STUB)
// - Decode identity + envelope + best-effort geometry binding
// - Maya/API free (uses raw Attributes/Connections captured in MayaNodeComponentBase)

using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya Deformer common base.
    /// Unity has no direct equivalent for most Maya deformers,
    /// so we preserve parameters + connections and provide best-effort reconstruction hooks.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class DeformerBase : MayaNodeComponentBase
    {
        // ===== Common identity =====

        [Header("Maya Deformer Common")]
        [Tooltip("Maya deformer name")]
        public string deformerName;

        [Tooltip("Maya node type")]
        public string mayaNodeType;

        [Tooltip("Maya node UUID")]
        public string mayaNodeUUID;

        [Tooltip("Envelope value (0-1)")]
        [Range(0f, 1f)]
        public float envelope = 1.0f;

        // ===== Geometry binding (best-effort) =====

        [Header("Geometry")]
        [Tooltip("Input geometry node name (best-effort from connections)")]
        public string inputGeometry;

        [Tooltip("Output geometry node name (best-effort from connections)")]
        public string outputGeometry;

        // ===== Weights (optional) =====

        [Header("Weights")]
        [Tooltip("Per-vertex weights (if applicable)")]
        public List<float> weights = new List<float>();

        // ===== Additional Attributes (non-serialized by Unity by default; raw attrs are still stored losslessly in Attributes) =====

        [Header("Additional Attributes (runtime use)")]
        public Dictionary<string, float> floatAttributes = new Dictionary<string, float>();
        public Dictionary<string, int> intAttributes = new Dictionary<string, int>();
        public Dictionary<string, bool> boolAttributes = new Dictionary<string, bool>();
        public Dictionary<string, string> stringAttributes = new Dictionary<string, string>();

        public void SetFloat(string attributeName, float value) => floatAttributes[attributeName] = value;
        public void SetInt(string attributeName, int value) => intAttributes[attributeName] = value;
        public void SetBool(string attributeName, bool value) => boolAttributes[attributeName] = value;
        public void SetString(string attributeName, string value) => stringAttributes[attributeName] = value;

        public bool TryGetFloat(string attributeName, out float value) => floatAttributes.TryGetValue(attributeName, out value);
        public bool TryGetInt(string attributeName, out int value) => intAttributes.TryGetValue(attributeName, out value);
        public bool TryGetBool(string attributeName, out bool value) => boolAttributes.TryGetValue(attributeName, out value);
        public bool TryGetString(string attributeName, out string value) => stringAttributes.TryGetValue(attributeName, out value);

        /// <summary>
        /// IMPORTANT:
        /// This override makes Deformer-derived nodes "not STUB" by providing real ApplyToUnity behavior.
        /// Derived classes should call base.ApplyToUnity(...) first, then decode their specifics.
        /// </summary>
        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // Identity
            deformerName = NodeName;
            mayaNodeType = NodeType;
            mayaNodeUUID = Uuid;

            // Envelope (common across deformers)
            envelope = Mathf.Clamp01(DeformerDecodeUtil.ReadFloat(this, envelope, ".envelope", "envelope", ".env", "env"));

            // Best-effort geometry binding from incoming connections
            // (deformers generally receive geometry on "input"/"inputGeometry"/"inMesh" etc)
            var inGeo = FindConnectedNodeByDstContains(
                "input", "inputGeometry", "inGeom", "inGeo", "inMesh", "inputMesh", "geom", "geometry");
            if (!string.IsNullOrEmpty(inGeo)) inputGeometry = inGeo;

            var outGeo = FindConnectedNodeByDstContains(
                "output", "outputGeometry", "outGeom", "outGeo", "outMesh", "outputMesh");
            if (!string.IsNullOrEmpty(outGeo)) outputGeometry = outGeo;

            log?.Info($"[deformer:{NodeType}] '{NodeName}' env={envelope:0.###} in={inputGeometry ?? "null"} out={outputGeometry ?? "null"}");
        }

        // ---------- connection helpers (best-effort) ----------

        protected string FindIncomingPlugByDstContains(params string[] patterns)
        {
            if (Connections == null || patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;

                    // contains match (fast + robust across variants)
                    if (dstAttr.Contains(pat, StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }

            return null;
        }

        protected string FindConnectedNodeByDstContains(params string[] patterns)
        {
            var plug = FindIncomingPlugByDstContains(patterns);
            if (string.IsNullOrEmpty(plug)) return null;
            return MayaPlugUtil.ExtractNodePart(plug);
        }

        protected static bool TryParseFloat(string token, out float value)
        {
            return float.TryParse(token, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
