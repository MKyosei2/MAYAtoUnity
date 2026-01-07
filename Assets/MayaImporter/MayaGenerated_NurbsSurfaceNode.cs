// PATCH: ProductionImpl v6 (Unity-only, retention-first)
// Phase-4 implementation: nurbsSurface -> Unity preview mesh (best-effort) + full CV preservation.
// NOTE: This is NOT a full NURBS evaluator. It reconstructs control vertices from .ma tokens and
// builds a deterministic low-res preview mesh via bilinear sampling over the CV lattice.
//
// 100% guarantee: raw Attributes/Connections remain in MayaNodeComponentBase.
// 100点(再構築)方針: Unityに概念が無いので専用ComponentとしてCV/weightを保持しつつ、最低限の可視化を行う。

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Generated
{
    [DisallowMultipleComponent]
    [MayaNodeType("nurbsSurface")]
    public sealed class MayaGenerated_NurbsSurfaceNode : MayaPhaseCNodeBase
    {
        [Header("Decoded (nurbsSurface best-effort)")]
        [SerializeField] private bool enabled = true;

        [Tooltip("Extracted CV lattice bounds (inclusive).")]
        [SerializeField] private int uMin = 0, uMax = -1, vMin = 0, vMax = -1;

        [Tooltip("Flattened CV positions in Maya space (row-major: u then v).")]
        [SerializeField] private Vector3[] controlVerticesMaya;

        [Tooltip("Flattened CV weights (same length as controlVerticesMaya). If not present, 1.0.")]
        [SerializeField] private float[] controlVertexWeights;

        [Header("Preview (Unity)")]
        [SerializeField] private bool buildPreviewMesh = true;

        [Range(2, 64)]
        [SerializeField] private int previewUSamples = 16;

        [Range(2, 64)]
        [SerializeField] private int previewVSamples = 16;

        [TextArea]
        [SerializeField] private string decodeSummary;

        protected override void DecodePhaseC(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            enabled = ReadBool(true, ".enabled", "enabled", ".enable", "enable");
            buildPreviewMesh = ReadBool(buildPreviewMesh, ".display", ".disp", ".draw", ".show", "display", "draw");

            // Extract CV lattice (best-effort)
            ExtractControlVertices(out controlVerticesMaya, out controlVertexWeights, out uMin, out uMax, out vMin, out vMax);

            int uCount = (uMax >= uMin) ? (uMax - uMin + 1) : 0;
            int vCount = (vMax >= vMin) ? (vMax - vMin + 1) : 0;

            decodeSummary =
                $"nurbsSurface decoded: enabled={enabled}, cvLattice=({uMin}:{uMax})x({vMin}:{vMax}) " +
                $"(uCount={uCount}, vCount={vCount}), cvLen={(controlVerticesMaya != null ? controlVerticesMaya.Length : 0)}, " +
                $"preview={(buildPreviewMesh ? "on" : "off")}";

            SetNotes(decodeSummary);

            if (!enabled)
                return;

            // Preserve as component (portfolio-proof) even if preview mesh can't be built.
            var cp = GetComponent<MayaNurbsSurfaceControlPointsComponent>() ?? gameObject.AddComponent<MayaNurbsSurfaceControlPointsComponent>();
            cp.uMin = uMin; cp.uMax = uMax; cp.vMin = vMin; cp.vMax = vMax;
            cp.controlVerticesMaya = controlVerticesMaya ?? Array.Empty<Vector3>();
            cp.controlVertexWeights = controlVertexWeights ?? Array.Empty<float>();

            if (!buildPreviewMesh)
                return;

            if (controlVerticesMaya == null || controlVerticesMaya.Length == 0 || uCount < 2 || vCount < 2)
            {
                log?.Warn($"[nurbsSurface] CV lattice missing/too small: '{NodeName}' (uCount={uCount}, vCount={vCount})");
                return;
            }

            // Build preview mesh (bilinear on CV lattice)
            var target = ResolveSurfaceTargetGO();
            var mesh = BuildPreviewMesh(cp, options, previewUSamples, previewVSamples);

            ApplyMeshToTarget(target, mesh);
            ApplyFallbackMaterial(target, NodeName);

            // Phase-7: mark this as a preview reconstruction (not a full NURBS evaluator).
            try
            {
                if (target != null)
                    MayaProvisionalMarker.Ensure(target, MayaProvisionalKind.NurbsSurfacePreviewMesh, decodeSummary);
            }
            catch { }
        }

        // =========================================================
        // CV extraction
        // =========================================================

        private void ExtractControlVertices(out Vector3[] cvs, out float[] weights, out int outUMin, out int outUMax, out int outVMin, out int outVMax)
        {
            cvs = null;
            weights = null;
            outUMin = 0; outUMax = -1; outVMin = 0; outVMax = -1;

            // PackedKey -> (pos, w)
            var map = new Dictionary<long, Vector4>(1024);

            bool any2D = false;

            for (int ai = 0; ai < Attributes.Count; ai++)
            {
                var a = Attributes[ai];
                if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                    continue;

                // Common keys:
                // ".cv[0:3][0:7]" / ".controlPoints[0:3][0:7]" / ".cp[...][...]"
                if (!LooksLikeCvKey(a.Key))
                    continue;

                if (!TryParse2DRange(a.Key, out int u0, out int u1, out int v0, out int v1, out bool is2D))
                    continue;

                any2D |= is2D;

                int uCount = (u1 - u0 + 1);
                int vCount = (v1 - v0 + 1);
                if (uCount <= 0 || vCount <= 0) continue;

                // tokens are floats: (x y z w) repeated OR (x y z) repeated
                var floats = CollectFloats(a.Tokens);
                if (floats.Count < 3) continue;

                int floatsPer = (floats.Count % 4 == 0) ? 4 : 3;
                int needed = uCount * vCount * floatsPer;
                if (floats.Count < needed)
                    continue;

                int t = 0;
                for (int u = 0; u < uCount; u++)
                {
                    for (int v = 0; v < vCount; v++)
                    {
                        float x = floats[t++]; float y = floats[t++]; float z = floats[t++];
                        float w = (floatsPer == 4) ? floats[t++] : 1f;

                        int uu = u0 + u;
                        int vv = v0 + v;

                        long key = PackKey(uu, vv);
                        map[key] = new Vector4(x, y, z, w);

                        if (outUMax < outUMin && outVMax < outVMin)
                        {
                            // init
                            outUMin = outUMax = uu;
                            outVMin = outVMax = vv;
                        }
                        else
                        {
                            outUMin = Mathf.Min(outUMin, uu);
                            outUMax = Mathf.Max(outUMax, uu);
                            outVMin = Mathf.Min(outVMin, vv);
                            outVMax = Mathf.Max(outVMax, vv);
                        }
                    }
                }
            }

            if (map.Count == 0)
            {
                // fallback: 1D packed CV array ".cv[0:...]" where v is always 0
                var map1 = new SortedDictionary<int, Vector4>();
                for (int ai = 0; ai < Attributes.Count; ai++)
                {
                    var a = Attributes[ai];
                    if (a == null || string.IsNullOrEmpty(a.Key) || a.Tokens == null || a.Tokens.Count == 0)
                        continue;

                    if (!LooksLikeCvKey(a.Key)) continue;
                    if (!TryParse1DRange(a.Key, out int s, out int e)) continue;
                    if (e < s) (s, e) = (e, s);

                    var floats = CollectFloats(a.Tokens);
                    int count = e - s + 1;
                    int floatsPer = (floats.Count % 4 == 0) ? 4 : 3;
                    int needed = count * floatsPer;
                    if (floats.Count < needed) continue;

                    int t = 0;
                    for (int i = 0; i < count; i++)
                    {
                        float x = floats[t++]; float y = floats[t++]; float z = floats[t++];
                        float w = (floatsPer == 4) ? floats[t++] : 1f;
                        map1[s + i] = new Vector4(x, y, z, w);
                    }
                }

                if (map1.Count == 0) return;

                int max = -1;
                foreach (var kv in map1) max = Mathf.Max(max, kv.Key);

                cvs = new Vector3[max + 1];
                weights = new float[max + 1];

                for (int i = 0; i < cvs.Length; i++) { cvs[i] = Vector3.zero; weights[i] = 1f; }

                foreach (var kv in map1)
                {
                    cvs[kv.Key] = new Vector3(kv.Value.x, kv.Value.y, kv.Value.z);
                    weights[kv.Key] = kv.Value.w;
                }

                outUMin = 0; outUMax = max;
                outVMin = 0; outVMax = 0;
                return;
            }

            // Build dense lattice (uCount*vCount) even if sparse: keep holes as zero/1.0.
            int U = (outUMax - outUMin + 1);
            int V = (outVMax - outVMin + 1);

            cvs = new Vector3[U * V];
            weights = new float[U * V];

            for (int i = 0; i < cvs.Length; i++) { cvs[i] = Vector3.zero; weights[i] = 1f; }

            for (int uu = outUMin; uu <= outUMax; uu++)
            {
                for (int vv = outVMin; vv <= outVMax; vv++)
                {
                    long key = PackKey(uu, vv);
                    if (map.TryGetValue(key, out var v4))
                    {
                        int idx = (uu - outUMin) * V + (vv - outVMin);
                        cvs[idx] = new Vector3(v4.x, v4.y, v4.z);
                        weights[idx] = v4.w;
                    }
                }
            }
        }

        private static bool LooksLikeCvKey(string key)
        {
            return key.Contains(".cv[", StringComparison.Ordinal) ||
                   key.Contains(".cp[", StringComparison.Ordinal) ||
                   key.Contains(".controlPoints[", StringComparison.Ordinal);
        }

        private static bool TryParse2DRange(string key, out int u0, out int u1, out int v0, out int v1, out bool is2D)
        {
            u0 = u1 = v0 = v1 = 0;
            is2D = false;

            if (string.IsNullOrEmpty(key)) return false;

            // find first '['..']'
            int lb1 = key.IndexOf('[', StringComparison.Ordinal);
            int rb1 = (lb1 >= 0) ? key.IndexOf(']', lb1 + 1) : -1;
            if (lb1 < 0 || rb1 < 0) return false;

            // second range (optional)
            int lb2 = key.IndexOf('[', rb1 + 1);
            int rb2 = (lb2 >= 0) ? key.IndexOf(']', lb2 + 1) : -1;

            if (!TryParseRange(key.Substring(lb1 + 1, rb1 - lb1 - 1), out u0, out u1))
                return false;

            if (lb2 < 0 || rb2 < 0)
            {
                // 1D only -> treat as v=0
                v0 = v1 = 0;
                is2D = false;
                return true;
            }

            if (!TryParseRange(key.Substring(lb2 + 1, rb2 - lb2 - 1), out v0, out v1))
                return false;

            is2D = true;
            return true;
        }

        private static bool TryParse1DRange(string key, out int s, out int e)
        {
            s = e = 0;
            if (string.IsNullOrEmpty(key)) return false;

            int lb = key.LastIndexOf('[', key.Length - 1);
            int rb = key.LastIndexOf(']', key.Length - 1);
            if (lb < 0 || rb < 0 || rb <= lb + 1) return false;

            return TryParseRange(key.Substring(lb + 1, rb - lb - 1), out s, out e);
        }

        private static bool TryParseRange(string inner, out int a, out int b)
        {
            a = b = 0;
            if (string.IsNullOrEmpty(inner)) return false;

            int colon = inner.IndexOf(':');
            if (colon < 0)
            {
                if (!int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out a))
                    return false;
                b = a;
                return true;
            }

            var s0 = inner.Substring(0, colon);
            var s1 = inner.Substring(colon + 1);
            if (!int.TryParse(s0, NumberStyles.Integer, CultureInfo.InvariantCulture, out a)) return false;
            if (!int.TryParse(s1, NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) return false;
            if (b < a) (a, b) = (b, a);
            return true;
        }

        private static List<float> CollectFloats(List<string> tokens)
        {
            var list = new List<float>(tokens != null ? tokens.Count : 0);
            if (tokens == null) return list;

            for (int i = 0; i < tokens.Count; i++)
            {
                var s = (tokens[i] ?? "").Trim();
                if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                {
                    if (float.IsNaN(f) || float.IsInfinity(f)) f = 0f;
                    list.Add(f);
                }
            }

            return list;
        }

        private static long PackKey(int u, int v)
        {
            unchecked
            {
                return ((long)u << 32) ^ (uint)v;
            }
        }

        // =========================================================
        // Preview mesh builder (bilinear on CV lattice)
        // =========================================================

        private static Mesh BuildPreviewMesh(MayaNurbsSurfaceControlPointsComponent cp, MayaImportOptions options, int uSamples, int vSamples)
        {
            int uMin = cp.uMin, uMax = cp.uMax, vMin = cp.vMin, vMax = cp.vMax;
            int U = (uMax - uMin + 1);
            int V = (vMax - vMin + 1);

            uSamples = Mathf.Clamp(uSamples, 2, 64);
            vSamples = Mathf.Clamp(vSamples, 2, 64);

            var verts = new List<Vector3>(uSamples * vSamples);
            var uv = new List<Vector2>(uSamples * vSamples);
            var tris = new List<int>((uSamples - 1) * (vSamples - 1) * 6);

            for (int iu = 0; iu < uSamples; iu++)
            {
                float fu = (uSamples <= 1) ? 0f : (iu / (float)(uSamples - 1));
                for (int iv = 0; iv < vSamples; iv++)
                {
                    float fv = (vSamples <= 1) ? 0f : (iv / (float)(vSamples - 1));

                    var pMaya = SampleBilinear(cp.controlVerticesMaya, U, V, fu, fv);
                    var pUnity = MayaToUnityConversion.ConvertPosition(pMaya, options.Conversion);

                    verts.Add(pUnity);
                    uv.Add(new Vector2(fu, fv));
                }
            }

            for (int iu = 0; iu < uSamples - 1; iu++)
            {
                for (int iv = 0; iv < vSamples - 1; iv++)
                {
                    int a = iu * vSamples + iv;
                    int b = (iu + 1) * vSamples + iv;
                    int c = (iu + 1) * vSamples + (iv + 1);
                    int d = iu * vSamples + (iv + 1);

                    // two tris (a,b,c) (a,c,d)
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                }
            }

            var m = new Mesh { name = "NurbsSurfacePreview" };
            if (verts.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            m.SetVertices(verts);
            m.SetUVs(0, uv);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            try { m.RecalculateTangents(); } catch { }
            return m;
        }

        private static Vector3 SampleBilinear(Vector3[] cv, int U, int V, float u, float v)
        {
            if (cv == null || cv.Length == 0 || U <= 0 || V <= 0)
                return Vector3.zero;

            // map u,v to lattice indices
            float x = u * (U - 1);
            float y = v * (V - 1);

            int x0 = Mathf.Clamp((int)Mathf.Floor(x), 0, U - 1);
            int y0 = Mathf.Clamp((int)Mathf.Floor(y), 0, V - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, U - 1);
            int y1 = Mathf.Clamp(y0 + 1, 0, V - 1);

            float tx = Mathf.Clamp01(x - x0);
            float ty = Mathf.Clamp01(y - y0);

            Vector3 p00 = cv[x0 * V + y0];
            Vector3 p10 = cv[x1 * V + y0];
            Vector3 p01 = cv[x0 * V + y1];
            Vector3 p11 = cv[x1 * V + y1];

            Vector3 a = Vector3.Lerp(p00, p10, tx);
            Vector3 b = Vector3.Lerp(p01, p11, tx);
            return Vector3.Lerp(a, b, ty);
        }

        // =========================================================
        // Target GO + renderer helpers
        // =========================================================

        private GameObject ResolveSurfaceTargetGO()
        {
            // Like mesh: attach to parent transform if this is a shape under a transform/joint.
            if (transform != null && transform.parent != null)
            {
                var p = transform.parent;
                var pn = p.GetComponent<MayaNodeComponentBase>();
                if (pn != null && (pn.NodeType == "transform" || pn.NodeType == "joint"))
                    return p.gameObject;
            }
            return gameObject;
        }

        private static void ApplyMeshToTarget(GameObject go, Mesh mesh)
        {
            if (go == null || mesh == null) return;

            var mf = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            if (go.GetComponent<MeshRenderer>() == null)
                go.AddComponent<MeshRenderer>();
        }

        private static void ApplyFallbackMaterial(GameObject go, string nameHint)
        {
            if (go == null) return;
            var mr = go.GetComponent<MeshRenderer>() ?? go.AddComponent<MeshRenderer>();

            if (mr.sharedMaterials != null && mr.sharedMaterials.Length > 0 && mr.sharedMaterials[0] != null)
                return;

            var sh = global::UnityEngine.Shader.Find("Universal Render Pipeline/Lit")
                     ?? global::UnityEngine.Shader.Find("Standard")
                     ?? global::UnityEngine.Shader.Find("Unlit/Color")
                     ?? global::UnityEngine.Shader.Find("Hidden/InternalErrorShader");

            var mat = new Material(sh) { name = "MAYA_NurbsSurface_" + (string.IsNullOrEmpty(nameHint) ? "Surface" : nameHint) };
            mr.sharedMaterials = new Material[] { mat };
        }
    }

    /// <summary>
    /// Unityに概念が無い nurbsSurface を「完全保持」するためのComponent。
    /// CV/weightをそのまま格納し、必要なら将来NURBS評価器に差し替え可能。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaNurbsSurfaceControlPointsComponent : MonoBehaviour
    {
        public int uMin, uMax, vMin, vMax;
        public Vector3[] controlVerticesMaya = Array.Empty<Vector3>();
        public float[] controlVertexWeights = Array.Empty<float>();
    }
}
