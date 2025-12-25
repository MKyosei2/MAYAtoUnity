using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Dynamics
{
    /// <summary>
    /// Base class for Maya field nodes -> MayaFieldRuntime (Unity-only).
    /// </summary>
    public abstract class MayaFieldNodeBase : MayaNodeComponentBase
    {
        protected abstract MayaFieldKind Kind { get; }

        [Header("Resolved (best-effort)")]
        public float magnitude = 1f;
        public float attenuation = 0f;
        public float maxDistance = 0f;
        public Vector3 direction = Vector3.forward;

        // special
        public float turbulenceFrequency = 0.5f;
        public float turbulenceSpeed = 1f;
        public Vector3 vortexAxis = Vector3.up;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            log ??= new MayaImportLog();

            magnitude = ReadFloat(".magnitude", ".mag", magnitude);
            attenuation = ReadFloat(".attenuation", ".att", attenuation);
            maxDistance = ReadFloat(".maxDistance", ".mxd", maxDistance);

            // direction: try packed, then xyz
            if (TryReadVec3(".direction", ".dir", out var d))
                direction = (d.sqrMagnitude > 1e-10f) ? d : direction;

            // specials (best-effort)
            turbulenceFrequency = ReadFloat(".frequency", ".freq", turbulenceFrequency);
            turbulenceSpeed = ReadFloat(".speed", ".spd", turbulenceSpeed);

            if (TryReadVec3(".axis", ".ax", out var ax))
                vortexAxis = (ax.sqrMagnitude > 1e-10f) ? ax : vortexAxis;

            var rt = GetComponent<MayaFieldRuntime>();
            if (rt == null) rt = gameObject.AddComponent<MayaFieldRuntime>();

            rt.SourceNodeName = NodeName;
            rt.Kind = Kind;
            rt.Magnitude = magnitude;
            rt.Attenuation = Mathf.Max(0f, attenuation);
            rt.MaxDistance = Mathf.Max(0f, maxDistance);
            rt.Direction = direction;
            rt.TurbulenceFrequency = turbulenceFrequency;
            rt.TurbulenceSpeed = turbulenceSpeed;
            rt.VortexAxis = vortexAxis;

            log.Info($"[field] '{NodeName}' kind={Kind} mag={magnitude} att={attenuation} maxD={maxDistance} dir={direction}");
        }

        protected float ReadFloat(string k1, string k2, float def)
        {
            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count > 0 &&
                float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f1))
                return f1;

            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count > 0 &&
                float.TryParse(a.Tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var f2))
                return f2;

            return def;
        }

        protected bool TryReadVec3(string k1, string k2, out Vector3 v)
        {
            v = Vector3.zero;

            if (TryGetAttr(k1, out var a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var x1) && TryF(a.Tokens[1], out var y1) && TryF(a.Tokens[2], out var z1))
            {
                v = new Vector3(x1, y1, z1);
                return true;
            }

            if (TryGetAttr(k2, out a) && a.Tokens != null && a.Tokens.Count >= 3 &&
                TryF(a.Tokens[0], out var x2) && TryF(a.Tokens[1], out var y2) && TryF(a.Tokens[2], out var z2))
            {
                v = new Vector3(x2, y2, z2);
                return true;
            }

            // xyz split fallback
            float x = ReadFloat(k1 + "X", k2 + "X", 0f);
            float y = ReadFloat(k1 + "Y", k2 + "Y", 0f);
            float z = ReadFloat(k1 + "Z", k2 + "Z", 0f);
            if (x != 0f || y != 0f || z != 0f)
            {
                v = new Vector3(x, y, z);
                return true;
            }

            return false;
        }

        private static bool TryF(string s, out float f)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    }
}
