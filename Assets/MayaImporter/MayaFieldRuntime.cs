using UnityEngine;

namespace MayaImporter.Dynamics
{
    public enum MayaFieldKind
    {
        Unknown = 0,
        Air,
        Gravity,
        Uniform,
        Drag,
        Turbulence,
        Vortex,
        Newton,
        Radial,
        Point,
        Curve,
        Surface,
        VolumeAxis
    }

    /// <summary>
    /// Unity-only runtime representation of Maya Field nodes.
    /// Best-effort force model (no Maya/API).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaFieldRuntime : MonoBehaviour
    {
        [Header("Source")]
        public string SourceNodeName;

        [Header("Kind")]
        public MayaFieldKind Kind = MayaFieldKind.Unknown;

        [Header("Common Params (best-effort)")]
        public float Magnitude = 1f;
        public float Attenuation = 0f;     // 0 = no falloff, >0 = stronger falloff
        public float MaxDistance = 0f;     // 0 = infinite
        public Vector3 Direction = Vector3.forward;

        [Header("Special (best-effort)")]
        public float TurbulenceFrequency = 0.5f;
        public float TurbulenceSpeed = 1f;
        public Vector3 VortexAxis = Vector3.up;

        public Vector3 ComputeAcceleration(Vector3 worldPos, Vector3 worldVel, float time)
        {
            float w = ComputeWeight(worldPos);
            if (w <= 0f) return Vector3.zero;

            float mag = Magnitude * w;

            switch (Kind)
            {
                case MayaFieldKind.Gravity:
                case MayaFieldKind.Uniform:
                    return SafeDir(Direction) * mag;

                case MayaFieldKind.Air:
                    // wind-like: accelerate towards wind direction
                    return SafeDir(Direction) * mag;

                case MayaFieldKind.Drag:
                    // drag: opposes velocity
                    return -worldVel * mag;

                case MayaFieldKind.Turbulence:
                    return Turbulence(worldPos, time) * mag;

                case MayaFieldKind.Vortex:
                    return Vortex(worldPos, worldVel) * mag;

                case MayaFieldKind.Newton:
                    // simple attraction to field origin
                    return Newton(worldPos) * mag;

                case MayaFieldKind.Radial:
                case MayaFieldKind.Point:
                    // push/pull along radial direction
                    return Radial(worldPos) * mag;

                // curve/surface/volumeAxis: best-effort fallback to direction
                case MayaFieldKind.Curve:
                case MayaFieldKind.Surface:
                case MayaFieldKind.VolumeAxis:
                    return SafeDir(Direction) * mag;

                default:
                    return Vector3.zero;
            }
        }

        private float ComputeWeight(Vector3 worldPos)
        {
            Vector3 d = worldPos - transform.position;
            float r = d.magnitude;

            if (MaxDistance > 0f && r > MaxDistance)
                return 0f;

            if (Attenuation <= 0f)
                return 1f;

            // best-effort falloff: 1 / (1 + att*r^2)
            return 1f / (1f + Attenuation * r * r);
        }

        private static Vector3 SafeDir(Vector3 v)
        {
            if (v.sqrMagnitude < 1e-12f) return Vector3.forward;
            return v.normalized;
        }

        private Vector3 Radial(Vector3 worldPos)
        {
            Vector3 r = worldPos - transform.position;
            if (r.sqrMagnitude < 1e-12f) return Vector3.zero;
            return r.normalized;
        }

        private Vector3 Newton(Vector3 worldPos)
        {
            Vector3 r = transform.position - worldPos;
            float rr = r.sqrMagnitude;
            if (rr < 1e-8f) return Vector3.zero;

            // inverse-square-ish (clamped)
            float inv = 1f / Mathf.Max(0.0005f, rr);
            return r.normalized * Mathf.Clamp(inv, 0f, 50f);
        }

        private Vector3 Vortex(Vector3 worldPos, Vector3 worldVel)
        {
            Vector3 axis = SafeDir(VortexAxis);
            Vector3 r = worldPos - transform.position;

            // project out axis
            r -= axis * Vector3.Dot(r, axis);
            if (r.sqrMagnitude < 1e-10f) return Vector3.zero;

            // tangential direction
            Vector3 tang = Vector3.Cross(axis, r).normalized;
            return tang;
        }

        private Vector3 Turbulence(Vector3 worldPos, float time)
        {
            // very cheap pseudo turbulence using PerlinNoise
            float f = Mathf.Max(0.0001f, TurbulenceFrequency);
            float t = time * TurbulenceSpeed;

            float nx = Mathf.PerlinNoise(worldPos.y * f + 11.3f, worldPos.z * f + t);
            float ny = Mathf.PerlinNoise(worldPos.z * f + 29.7f, worldPos.x * f + t);
            float nz = Mathf.PerlinNoise(worldPos.x * f + 47.1f, worldPos.y * f + t);

            // map 0..1 to -1..1
            return new Vector3(nx * 2f - 1f, ny * 2f - 1f, nz * 2f - 1f);
        }
    }
}
