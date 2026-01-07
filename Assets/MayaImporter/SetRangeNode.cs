// MayaImporter/SetRangeNode.cs
// NodeType: setRange
// Production (real decode + value publish)
//
// Maya setRange (best-effort):
// out = min + (max-min) * ((value-oldMin)/(oldMax-oldMin))
// optional clamp (attr: clamp)
//
// Publishes:
// - MayaVector3Value : outValue (xyz)
// - MayaFloatValue   : outX as convenience

using System.Globalization;
using UnityEngine;
using MayaImporter;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    [DisallowMultipleComponent]
    [MayaNodeType("setRange")]
    public sealed class SetRangeNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (setRange)")]
        [SerializeField] private bool enabled = true;

        [SerializeField] private bool clamp = true;

        [SerializeField] private Vector3 value = Vector3.zero;
        [SerializeField] private Vector3 oldMin = Vector3.zero;
        [SerializeField] private Vector3 oldMax = Vector3.one;
        [SerializeField] private Vector3 min = Vector3.zero;
        [SerializeField] private Vector3 max = Vector3.one;

        [Header("Incoming (best-effort)")]
        [SerializeField] private string incomingValue;
        [SerializeField] private string incomingOldMin;
        [SerializeField] private string incomingOldMax;
        [SerializeField] private string incomingMin;
        [SerializeField] private string incomingMax;

        [Header("Output (computed)")]
        [SerializeField] private Vector3 outValue = Vector3.zero;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            bool muted = ReadBool(false, ".mute", "mute", ".disabled", "disabled");
            bool explicitEnabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            enabled = !muted && explicitEnabled;

            clamp = ReadBool(true, ".clamp", "clamp", ".cl", "cl");

            value = ReadVec3(def: Vector3.zero, baseName: "value");
            oldMin = ReadVec3(def: Vector3.zero, baseName: "oldMin");
            oldMax = ReadVec3(def: Vector3.one, baseName: "oldMax");
            min = ReadVec3(def: Vector3.zero, baseName: "min");
            max = ReadVec3(def: Vector3.one, baseName: "max");

            incomingValue = FindLastIncomingTo("value", "valueX", "valueY", "valueZ");
            incomingOldMin = FindLastIncomingTo("oldMin", "oldMinX", "oldMinY", "oldMinZ");
            incomingOldMax = FindLastIncomingTo("oldMax", "oldMaxX", "oldMaxY", "oldMaxZ");
            incomingMin = FindLastIncomingTo("min", "minX", "minY", "minZ");
            incomingMax = FindLastIncomingTo("max", "maxX", "maxY", "maxZ");

            if (!enabled)
            {
                outValue = Vector3.zero;
                Publish();
                SetNotes($"{NodeType} '{NodeName}' disabled.");
                return;
            }

            outValue = new Vector3(
                Eval(value.x, oldMin.x, oldMax.x, min.x, max.x, clamp),
                Eval(value.y, oldMin.y, oldMax.y, min.y, max.y, clamp),
                Eval(value.z, oldMin.z, oldMax.z, min.z, max.z, clamp)
            );

            Publish();

            SetNotes(
                $"{NodeType} '{NodeName}' enabled clamp={clamp} " +
                $"value=({value.x:0.###},{value.y:0.###},{value.z:0.###}) " +
                $"oldMin=({oldMin.x:0.###},{oldMin.y:0.###},{oldMin.z:0.###}) oldMax=({oldMax.x:0.###},{oldMax.y:0.###},{oldMax.z:0.###}) " +
                $"min=({min.x:0.###},{min.y:0.###},{min.z:0.###}) max=({max.x:0.###},{max.y:0.###},{max.z:0.###}) " +
                $"out=({outValue.x:0.###},{outValue.y:0.###},{outValue.z:0.###})"
            );
        }

        private void Publish()
        {
            var v3 = GetComponent<MayaVector3Value>() ?? gameObject.AddComponent<MayaVector3Value>();
            v3.Set(MayaVector3Value.Kind.Vector, outValue, outValue);

            var f = GetComponent<MayaFloatValue>() ?? gameObject.AddComponent<MayaFloatValue>();
            f.Set(outValue.x);
        }

        private static float Eval(float v, float oldMin, float oldMax, float min, float max, bool clamp01)
        {
            float denom = oldMax - oldMin;
            if (Mathf.Abs(denom) < 1e-8f)
                return min;

            float t = (v - oldMin) / denom;

            if (clamp01)
                t = Mathf.Clamp01(t);

            return min + (max - min) * t;
        }

        private Vector3 ReadVec3(Vector3 def, string baseName)
        {
            // packed: ".value" / "value" etc
            string dotPacked = "." + baseName;
            if (TryGetTokens(dotPacked, out var t0) && t0 != null && t0.Count >= 3)
            {
                if (TryParseFloat(t0[0], out var x) &&
                    TryParseFloat(t0[1], out var y) &&
                    TryParseFloat(t0[2], out var z))
                    return new Vector3(x, y, z);
            }

            if (TryGetTokens(baseName, out var t1) && t1 != null && t1.Count >= 3)
            {
                if (TryParseFloat(t1[0], out var x) &&
                    TryParseFloat(t1[1], out var y) &&
                    TryParseFloat(t1[2], out var z))
                    return new Vector3(x, y, z);
            }

            // axis fallback
            float xx = ReadFloat(def.x, "." + baseName + "X", baseName + "X");
            float yy = ReadFloat(def.y, "." + baseName + "Y", baseName + "Y");
            float zz = ReadFloat(def.z, "." + baseName + "Z", baseName + "Z");
            return new Vector3(xx, yy, zz);
        }

        private static bool TryParseFloat(string s, out float v)
        {
            return float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
