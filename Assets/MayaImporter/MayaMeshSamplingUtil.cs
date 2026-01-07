// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Constraints
{
    /// <summary>
    /// Mesh sampling helpers (closest-point and UV-in-triangle sampling).
    /// No Maya/API required.
    /// </summary>
    public static class MayaMeshSamplingUtil
    {
        public struct Sample
        {
            public bool Valid;
            public Vector3 PositionWS;
            public Vector3 NormalWS;
            public Vector3 TangentWS;
        }

        public static bool TryGetMesh(Transform root, out Mesh mesh, out Transform meshTransform, out bool isSkinned)
        {
            mesh = null;
            meshTransform = null;
            isSkinned = false;

            if (root == null) return false;

            // Prefer SkinnedMeshRenderer (deformed meshes)
            var smr = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null)
            {
                mesh = smr.sharedMesh;
                meshTransform = smr.transform;
                isSkinned = true;
                return true;
            }

            // MeshFilter
            var mf = root.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && mf.sharedMesh != null)
            {
                mesh = mf.sharedMesh;
                meshTransform = mf.transform;
                isSkinned = false;
                return true;
            }

            return false;
        }

        public static Sample ClosestPointOnMeshWS(Mesh mesh, Transform meshTf, Vector3 queryWS)
        {
            var s = new Sample { Valid = false };
            if (mesh == null || meshTf == null) return s;

            var verts = mesh.vertices;
            var tris = mesh.triangles;
            if (verts == null || tris == null || verts.Length == 0 || tris.Length < 3) return s;

            var normals = mesh.normals;
            bool hasNormals = normals != null && normals.Length == verts.Length;

            var tangents = mesh.tangents;
            bool hasTangents = tangents != null && tangents.Length == verts.Length;

            float best = float.PositiveInfinity;
            Vector3 bestP = default, bestN = Vector3.up, bestT = Vector3.forward;

            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i];
                int i1 = tris[i + 1];
                int i2 = tris[i + 2];
                if ((uint)i0 >= (uint)verts.Length || (uint)i1 >= (uint)verts.Length || (uint)i2 >= (uint)verts.Length)
                    continue;

                Vector3 p0 = meshTf.TransformPoint(verts[i0]);
                Vector3 p1 = meshTf.TransformPoint(verts[i1]);
                Vector3 p2 = meshTf.TransformPoint(verts[i2]);

                Vector3 cp = ClosestPointOnTriangle(queryWS, p0, p1, p2, out var bary);
                float d2 = (cp - queryWS).sqrMagnitude;

                if (d2 < best)
                {
                    best = d2;
                    bestP = cp;

                    // normal
                    if (hasNormals)
                    {
                        Vector3 n0 = meshTf.TransformDirection(normals[i0]);
                        Vector3 n1 = meshTf.TransformDirection(normals[i1]);
                        Vector3 n2 = meshTf.TransformDirection(normals[i2]);
                        bestN = (n0 * bary.x + n1 * bary.y + n2 * bary.z);
                    }
                    else
                    {
                        bestN = Vector3.Cross(p1 - p0, p2 - p0);
                    }

                    // tangent
                    if (hasTangents)
                    {
                        Vector3 t0 = meshTf.TransformDirection(new Vector3(tangents[i0].x, tangents[i0].y, tangents[i0].z));
                        Vector3 t1 = meshTf.TransformDirection(new Vector3(tangents[i1].x, tangents[i1].y, tangents[i1].z));
                        Vector3 t2 = meshTf.TransformDirection(new Vector3(tangents[i2].x, tangents[i2].y, tangents[i2].z));
                        bestT = (t0 * bary.x + t1 * bary.y + t2 * bary.z);
                    }
                    else
                    {
                        bestT = (p1 - p0);
                    }
                }
            }

            if (!float.IsFinite(best)) return s;

            if (bestN.sqrMagnitude < 1e-12f) bestN = Vector3.up;
            if (bestT.sqrMagnitude < 1e-12f) bestT = Vector3.forward;

            s.Valid = true;
            s.PositionWS = bestP;
            s.NormalWS = bestN.normalized;
            s.TangentWS = bestT.normalized;
            return s;
        }

        public static Sample SampleByUVOnMeshWS(Mesh mesh, Transform meshTf, float u, float v)
        {
            var s = new Sample { Valid = false };
            if (mesh == null || meshTf == null) return s;

            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var uvs = mesh.uv;
            if (verts == null || tris == null || uvs == null) return s;
            if (verts.Length == 0 || tris.Length < 3 || uvs.Length != verts.Length) return s;

            var normals = mesh.normals;
            bool hasNormals = normals != null && normals.Length == verts.Length;

            var tangents = mesh.tangents;
            bool hasTangents = tangents != null && tangents.Length == verts.Length;

            Vector2 q = new Vector2(u, v);

            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i];
                int i1 = tris[i + 1];
                int i2 = tris[i + 2];
                if ((uint)i0 >= (uint)verts.Length || (uint)i1 >= (uint)verts.Length || (uint)i2 >= (uint)verts.Length)
                    continue;

                Vector2 uv0 = uvs[i0];
                Vector2 uv1 = uvs[i1];
                Vector2 uv2 = uvs[i2];

                if (!TryBarycentricUV(q, uv0, uv1, uv2, out var bary))
                    continue;

                Vector3 p0 = meshTf.TransformPoint(verts[i0]);
                Vector3 p1 = meshTf.TransformPoint(verts[i1]);
                Vector3 p2 = meshTf.TransformPoint(verts[i2]);
                Vector3 pos = p0 * bary.x + p1 * bary.y + p2 * bary.z;

                Vector3 n;
                if (hasNormals)
                {
                    Vector3 n0 = meshTf.TransformDirection(normals[i0]);
                    Vector3 n1 = meshTf.TransformDirection(normals[i1]);
                    Vector3 n2 = meshTf.TransformDirection(normals[i2]);
                    n = (n0 * bary.x + n1 * bary.y + n2 * bary.z);
                }
                else
                {
                    n = Vector3.Cross(p1 - p0, p2 - p0);
                }

                Vector3 t;
                if (hasTangents)
                {
                    Vector3 t0 = meshTf.TransformDirection(new Vector3(tangents[i0].x, tangents[i0].y, tangents[i0].z));
                    Vector3 t1 = meshTf.TransformDirection(new Vector3(tangents[i1].x, tangents[i1].y, tangents[i1].z));
                    Vector3 t2 = meshTf.TransformDirection(new Vector3(tangents[i2].x, tangents[i2].y, tangents[i2].z));
                    t = (t0 * bary.x + t1 * bary.y + t2 * bary.z);
                }
                else
                {
                    t = (p1 - p0);
                }

                if (n.sqrMagnitude < 1e-12f) n = Vector3.up;
                if (t.sqrMagnitude < 1e-12f) t = Vector3.forward;

                s.Valid = true;
                s.PositionWS = pos;
                s.NormalWS = n.normalized;
                s.TangentWS = t.normalized;
                return s;
            }

            return s;
        }

        private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
        {
            // From "Real-Time Collision Detection" (Christer Ericson) style
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 ap = p - a;

            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { bary = new Vector3(1f, 0f, 0f); return a; }

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { bary = new Vector3(0f, 1f, 0f); return b; }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                bary = new Vector3(1f - v, v, 0f);
                return a + v * ab;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { bary = new Vector3(0f, 0f, 1f); return c; }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                bary = new Vector3(1f - w, 0f, w);
                return a + w * ac;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                bary = new Vector3(0f, 1f - w, w);
                return b + w * (c - b);
            }

            // Inside face region
            float denom = 1f / (va + vb + vc);
            float v2 = vb * denom;
            float w2 = vc * denom;
            float u2 = 1f - v2 - w2;
            bary = new Vector3(u2, v2, w2);
            return a * u2 + b * v2 + c * w2;
        }

        private static bool TryBarycentricUV(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector3 bary)
        {
            // Barycentric in 2D
            Vector2 v0 = b - a;
            Vector2 v1 = c - a;
            Vector2 v2 = p - a;

            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);

            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-12f)
            {
                bary = default;
                return false;
            }

            float inv = 1f / denom;
            float v = (d11 * d20 - d01 * d21) * inv;
            float w = (d00 * d21 - d01 * d20) * inv;
            float u = 1f - v - w;

            // inside triangle (with tiny tolerance)
            const float eps = -1e-4f;
            if (u >= eps && v >= eps && w >= eps)
            {
                bary = new Vector3(u, v, w);
                return true;
            }

            bary = default;
            return false;
        }
    }
}
