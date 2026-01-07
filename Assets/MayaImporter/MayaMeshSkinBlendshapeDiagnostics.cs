// Assets/MayaImporter/MayaMeshSkinBlendshapeDiagnostics.cs
using System;
using System.Text;
using UnityEngine;

namespace MayaImporter.Portfolio
{
    /// <summary>
    /// Skin / BlendShape diagnostics for portfolio proof:
    /// - Outputs basic mesh stats
    /// - Outputs checksum64 of vertices/normals/tangents/uv/boneWeights/blendShapes
    /// - Performs basic validity checks for bone weight sums
    /// </summary>
    public static class MayaMeshSkinBlendshapeDiagnostics
    {
        public static string BuildReport(GameObject root,
            bool includeVertices = true,
            bool includeNormals = true,
            bool includeTangents = true,
            bool includeUV0 = true,
            bool includeBoneWeights = true,
            bool includeBlendShapes = true)
        {
            if (root == null) return "Root is null.";

            var sb = new StringBuilder(32 * 1024);
            sb.AppendLine("Mesh / Skin / BlendShape Diagnostics");
            sb.AppendLine("===================================");
            sb.AppendLine($"Root: {root.name}");
            sb.AppendLine();

            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mrs = root.GetComponentsInChildren<MeshRenderer>(true);

            sb.AppendLine($"SkinnedMeshRenderer: {smrs?.Length ?? 0}");
            sb.AppendLine($"MeshRenderer       : {mrs?.Length ?? 0}");
            sb.AppendLine();

            if (smrs != null)
            {
                for (int i = 0; i < smrs.Length; i++)
                {
                    var smr = smrs[i];
                    if (smr == null) continue;

                    sb.AppendLine($"[SMR {i}] {smr.name}");
                    sb.AppendLine($"  bones: {smr.bones?.Length ?? 0}");
                    sb.AppendLine($"  rootBone: {(smr.rootBone != null ? smr.rootBone.name : "(null)")}");

                    var mesh = smr.sharedMesh;
                    if (mesh == null)
                    {
                        sb.AppendLine("  Mesh: (null)");
                        sb.AppendLine();
                        continue;
                    }

                    AppendMeshReport(sb, mesh, includeVertices, includeNormals, includeTangents, includeUV0, includeBoneWeights, includeBlendShapes);
                    sb.AppendLine();
                }
            }

            // Also report non-skinned meshes briefly (for completeness)
            if (mrs != null)
            {
                for (int i = 0; i < mrs.Length; i++)
                {
                    var mr = mrs[i];
                    if (mr == null) continue;

                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    sb.AppendLine($"[MR {i}] {mr.name}");
                    AppendMeshReport(sb, mf.sharedMesh, includeVertices, includeNormals, includeTangents, includeUV0, includeBoneWeights: false, includeBlendShapes: includeBlendShapes);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static void AppendMeshReport(StringBuilder sb, Mesh mesh,
            bool includeVertices, bool includeNormals, bool includeTangents, bool includeUV0,
            bool includeBoneWeights, bool includeBlendShapes)
        {
            sb.AppendLine($"  Mesh: {mesh.name}");
            sb.AppendLine($"  vertices={mesh.vertexCount}  subMeshes={mesh.subMeshCount}  triangles={CountTriangles(mesh)}  blendShapes={mesh.blendShapeCount}");

            ulong h = 1469598103934665603UL; // FNV-1a offset basis
            h = HashString(h, mesh.name);
            h = HashInt(h, mesh.vertexCount);
            h = HashInt(h, mesh.subMeshCount);
            h = HashInt(h, mesh.blendShapeCount);

            if (includeVertices) h = HashVector3Array(h, mesh.vertices);
            if (includeNormals) h = HashVector3Array(h, mesh.normals);
            if (includeTangents) h = HashVector4Array(h, mesh.tangents);
            if (includeUV0) h = HashVector2Array(h, mesh.uv);

            if (includeBoneWeights)
            {
                var bw = mesh.boneWeights;
                h = HashBoneWeights(h, bw);

                // 三項演算子は補間内で必ず () で括る（CS8361対策）
                sb.AppendLine($"  boneWeights: {(bw != null ? bw.Length : 0)}");

                if (bw != null && bw.Length > 0)
                {
                    float maxSumErr = 0f;
                    int bad = 0;
                    for (int bi = 0; bi < bw.Length; bi++)
                    {
                        float sum = bw[bi].weight0 + bw[bi].weight1 + bw[bi].weight2 + bw[bi].weight3;
                        float err = Mathf.Abs(1f - sum);
                        if (err > maxSumErr) maxSumErr = err;
                        if (err > 0.01f) bad++;
                    }
                    sb.AppendLine($"  weightSumMaxError={maxSumErr:0.000000}  bad(>0.01)={bad}");
                }
            }

            if (includeBlendShapes && mesh.blendShapeCount > 0)
            {
                var dv = new Vector3[mesh.vertexCount];
                var dn = new Vector3[mesh.vertexCount];
                var dt = new Vector3[mesh.vertexCount];

                for (int si = 0; si < mesh.blendShapeCount; si++)
                {
                    string n = mesh.GetBlendShapeName(si);
                    int frames = mesh.GetBlendShapeFrameCount(si);

                    h = HashString(h, n);
                    h = HashInt(h, frames);

                    for (int fi = 0; fi < frames; fi++)
                    {
                        float w = mesh.GetBlendShapeFrameWeight(si, fi);
                        h = HashFloat(h, w);

                        mesh.GetBlendShapeFrameVertices(si, fi, dv, dn, dt);
                        h = HashVector3Array(h, dv);
                        h = HashVector3Array(h, dn);
                        h = HashVector3Array(h, dt);
                    }
                }
            }

            sb.AppendLine($"  checksum64=0x{h:X16}");
        }

        private static int CountTriangles(Mesh m)
        {
            int tris = 0;
            try
            {
                for (int i = 0; i < m.subMeshCount; i++)
                    tris += (m.GetTriangles(i)?.Length ?? 0) / 3;
            }
            catch { /* ignore */ }
            return tris;
        }

        // =========================
        // Hash helpers (FNV-1a 64)
        // =========================
        private static ulong HashByte(ulong h, byte b)
        {
            h ^= b;
            h *= 1099511628211UL;
            return h;
        }

        private static ulong HashInt(ulong h, int v)
        {
            unchecked
            {
                h = HashByte(h, (byte)(v));
                h = HashByte(h, (byte)(v >> 8));
                h = HashByte(h, (byte)(v >> 16));
                h = HashByte(h, (byte)(v >> 24));
            }
            return h;
        }

        private static ulong HashFloat(ulong h, float f)
        {
            var bytes = BitConverter.GetBytes(f);
            for (int i = 0; i < bytes.Length; i++) h = HashByte(h, bytes[i]);
            return h;
        }

        private static ulong HashString(ulong h, string s)
        {
            if (string.IsNullOrEmpty(s)) return HashInt(h, 0);
            var bytes = Encoding.UTF8.GetBytes(s);
            for (int i = 0; i < bytes.Length; i++) h = HashByte(h, bytes[i]);
            return h;
        }

        private static ulong HashVector2Array(ulong h, Vector2[] a)
        {
            if (a == null) return HashInt(h, 0);
            h = HashInt(h, a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                h = HashFloat(h, a[i].x);
                h = HashFloat(h, a[i].y);
            }
            return h;
        }

        private static ulong HashVector3Array(ulong h, Vector3[] a)
        {
            if (a == null) return HashInt(h, 0);
            h = HashInt(h, a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                h = HashFloat(h, a[i].x);
                h = HashFloat(h, a[i].y);
                h = HashFloat(h, a[i].z);
            }
            return h;
        }

        private static ulong HashVector4Array(ulong h, Vector4[] a)
        {
            if (a == null) return HashInt(h, 0);
            h = HashInt(h, a.Length);
            for (int i = 0; i < a.Length; i++)
            {
                h = HashFloat(h, a[i].x);
                h = HashFloat(h, a[i].y);
                h = HashFloat(h, a[i].z);
                h = HashFloat(h, a[i].w);
            }
            return h;
        }

        private static ulong HashBoneWeights(ulong h, BoneWeight[] bw)
        {
            if (bw == null) return HashInt(h, 0);
            h = HashInt(h, bw.Length);
            for (int i = 0; i < bw.Length; i++)
            {
                h = HashInt(h, bw[i].boneIndex0);
                h = HashInt(h, bw[i].boneIndex1);
                h = HashInt(h, bw[i].boneIndex2);
                h = HashInt(h, bw[i].boneIndex3);

                h = HashFloat(h, bw[i].weight0);
                h = HashFloat(h, bw[i].weight1);
                h = HashFloat(h, bw[i].weight2);
                h = HashFloat(h, bw[i].weight3);
            }
            return h;
        }
    }
}
