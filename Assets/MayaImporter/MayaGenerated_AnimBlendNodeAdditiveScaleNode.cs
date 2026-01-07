// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// NodeType: animBlendNodeAdditiveScale
// Production: meaningful decode + best-effort evaluation (Vector3 scale)
//
// Best-effort semantics:
//  out = inputA * weightA + inputB * weightB
//
// Publishes output via MayaVector3Value (Core), Kind=Vector.

using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("animBlendNodeAdditiveScale")]
    public sealed class MayaGenerated_AnimBlendNodeAdditiveScaleNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (animBlendNodeAdditiveScale)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private Vector3 inputA;
        [SerializeField] private Vector3 inputB;

        [SerializeField] private float weightA = 1f;
        [SerializeField] private float weightB = 1f;
        [SerializeField] private float weight = 1f;

        [SerializeField] private Vector3 output;

        [Header("Incoming Plugs (best-effort)")]
        [SerializeField] private string srcInputAPlug;
        [SerializeField] private string srcInputBPlug;
        [SerializeField] private string srcWeightPlug;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            inputA = ReadVec3(Vector3.one,
                packed: new[] { ".inputA", "inputA", ".ia", "ia", ".input", "input" },
                xKeys: new[] { ".inputAX", "inputAX", ".iax", "iax", ".inputX", "inputX" },
                yKeys: new[] { ".inputAY", "inputAY", ".iay", "iay", ".inputY", "inputY" },
                zKeys: new[] { ".inputAZ", "inputAZ", ".iaz", "iaz", ".inputZ", "inputZ" });

            inputB = ReadVec3(Vector3.zero,
                packed: new[] { ".inputB", "inputB", ".ib", "ib", ".additive", "additive" },
                xKeys: new[] { ".inputBX", "inputBX", ".ibx", "ibx" },
                yKeys: new[] { ".inputBY", "inputBY", ".iby", "iby" },
                zKeys: new[] { ".inputBZ", "inputBZ", ".ibz", "ibz" });

            weightA = ReadFloat(1f, ".weightA", "weightA", ".wa", "wa");
            weightB = ReadFloat(float.NaN, ".weightB", "weightB", ".wb", "wb");
            weight = ReadFloat(1f, ".weight", "weight", ".w", "w", ".envelope", "envelope", ".env", "env");
            if (float.IsNaN(weightB)) weightB = weight;

            srcInputAPlug = FindIncomingPlugContains("inputA", "ia", "input");
            srcInputBPlug = FindIncomingPlugContains("inputB", "ib", "additive");
            srcWeightPlug = FindIncomingPlugContains("weight", ".w", "envelope", "env");

            output = enabled ? (inputA * weightA + inputB * weightB) : inputA;

            // publish vector (no coordinate conversion for scale here)
            var outVal = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            outVal.Set(MayaVector3Value.Kind.Vector, output, output);

            var meta = GetComponent<MayaAnimBlendAdditiveVectorMetadata_Scale>() ?? gameObject.AddComponent<MayaAnimBlendAdditiveVectorMetadata_Scale>();
            meta.valid = true;
            meta.enabled = enabled;
            meta.inputA = inputA;
            meta.inputB = inputB;
            meta.weightA = weightA;
            meta.weightB = weightB;
            meta.output = output;
            meta.srcInputAPlug = srcInputAPlug;
            meta.srcInputBPlug = srcInputBPlug;
            meta.srcWeightPlug = srcWeightPlug;
            meta.lastBuildFrame = Time.frameCount;

            SetNotes($"{NodeType} '{NodeName}' decoded: enabled={enabled}, inA={inputA}, inB={inputB}, wa={weightA:0.###}, wb={weightB:0.###}, out={output}");
            log.Info($"[animBlendNodeAdditiveScale] '{NodeName}' enabled={enabled} wa={weightA:0.###} wb={weightB:0.###} out=({output.x:0.###},{output.y:0.###},{output.z:0.###})");
        }

        private Vector3 ReadVec3(Vector3 def, string[] packed, string[] xKeys, string[] yKeys, string[] zKeys)
        {
            // packed: 3 tokens
            if (packed != null)
            {
                for (int i = 0; i < packed.Length; i++)
                {
                    if (TryGetTokens(packed[i], out var t) && t != null && t.Count >= 3 &&
                        float.TryParse(t[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                        float.TryParse(t[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                        float.TryParse(t[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                        return new Vector3(x, y, z);
                }
            }

            float xx = ReadFloat(def.x, xKeys);
            float yy = ReadFloat(def.y, yKeys);
            float zz = ReadFloat(def.z, zKeys);
            return new Vector3(xx, yy, zz);
        }

        private float ReadFloat(float def, params string[] keys)
        {
            if (keys == null) return def;
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetTokens(keys[i], out var t) && t != null && t.Count > 0)
                {
                    // prefer last numeric token
                    for (int j = t.Count - 1; j >= 0; j--)
                    {
                        var s = (t[j] ?? "").Trim();
                        if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                            return f;
                    }
                }
            }
            return def;
        }

        private string FindIncomingPlugContains(params string[] patterns)
        {
            if (Connections == null || Connections.Count == 0) return null;
            if (patterns == null || patterns.Length == 0) return null;

            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                var c = Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != ConnectionRole.Destination &&
                    c.RoleForThisNode != ConnectionRole.Both)
                    continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(c.DstPlug);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pat = patterns[p];
                    if (string.IsNullOrEmpty(pat)) continue;
                    if (dstAttr.Contains(pat, System.StringComparison.Ordinal))
                        return c.SrcPlug;
                }
            }
            return null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class MayaAnimBlendAdditiveVectorMetadata_Scale : MonoBehaviour
    {
        public bool valid;
        public bool enabled;

        public Vector3 inputA;
        public Vector3 inputB;
        public float weightA;
        public float weightB;
        public Vector3 output;

        public string srcInputAPlug;
        public string srcInputBPlug;
        public string srcWeightPlug;

        public int lastBuildFrame;
    }
}
