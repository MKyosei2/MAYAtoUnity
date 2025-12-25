using System;
using System.Collections.Generic;
using System.Globalization;
using MayaImporter.Core;
using MayaImporter.Components;
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// NodeType: mesh
    ///
    /// 100%方針:
    /// - Unity Mesh に落ちる範囲は確実に構築
    /// - Unityに概念が無い/制限がある要素(holes, UV8+, face-varying normals mapping不明など)は
    ///   失敗にせず、RawTokens(Attributes) + 補助Componentで保持し「GameObject再構築」を継続する
    /// </summary>
    [MayaNodeType("mesh")]
    [DisallowMultipleComponent]
    public sealed class MayaMeshNodeComponent : MayaNodeComponentBase
    {
        [Header("Mesh Build Result")]
        public bool BuiltMesh;
        public string BuildNote;

        [Header("Topology Mapping")]
        [Tooltip("Expanded vertex index -> original Maya vertex index (vt[] index). Used by deformers (blendShape/skin).")]
        public int[] ExpandedToOriginalVertex;

        [Header("Extras (Unity has no direct concept)")]
        public bool HasFaceHoles;
        public int HoleFaceCount;
        public bool HadFaces;
        public int DetectedUvSetCount;
        public bool UsedFaceVaryingNormals;

        public override void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            if (BuiltMesh) return;

            var scene = MayaBuildContext.CurrentScene;
            var targetGO = ResolveMeshTargetGO();

            // =========================================================
            // .mb path (no Maya installed)
            // =========================================================
            if (scene != null &&
                !string.IsNullOrEmpty(scene.SourcePath) &&
                scene.SourcePath.EndsWith(".mb", StringComparison.OrdinalIgnoreCase) &&
                scene.RawBinaryBytes != null && scene.RawBinaryBytes.Length > 0)
            {
                var rec = FindNodeRecordByAnyName(scene, NodeName);
                if (rec != null)
                {
                    if (MayaMbMeshDecoder.TryBuildUnityMesh(
                            scene.RawBinaryBytes,
                            rec,
                            options,
                            out var mbMesh,
                            out var expandedToOriginal,
                            out var debugNote))
                    {
                        ApplyUnityMeshToTarget(targetGO, mbMesh);

                        ExpandedToOriginalVertex = expandedToOriginal;
                        BuiltMesh = true;
                        BuildNote = $"MB: {debugNote}";
                        log?.Info("[mesh/.mb] " + BuildNote);
                        return;
                    }
                }
            }

            // =========================================================
            // ASCII (.ma) path
            // =========================================================

            // vertices
            if (!TryCollectFloat3Array("vt[", out var vertsMaya))
            {
                // verticesすら無い mesh は、空Meshを作ってGameObject再構築を成立させる
                var empty = new Mesh { name = string.IsNullOrEmpty(NodeName) ? "MayaMesh_Empty" : (NodeName + "_Empty") };
                ApplyUnityMeshToTarget(targetGO, empty);

                BuiltMesh = true;
                HadFaces = false;
                BuildNote = "No vertices found (expected vt[...]). Created empty Mesh to preserve node.";
                return;
            }

            // edges (for hard-edge smoothing groups)
            TryCollectEdges(out var edges);

            // normals pool (may be per-vertex OR separate pool)
            TryCollectFloat3Array("n[", out var normalsPoolMaya);

            // colors (optional; only use if matches vertex count)
            TryCollectFloat4Array("clr[", out var colorsPool);
            bool canUseVertexColors = (colorsPool != null && colorsPool.Length == vertsMaya.Length);

            // UV pools (all uvst[*])
            var uvPools = CollectUvPools(out var uvSetMaxAttr);
            DetectedUvSetCount = (uvSetMaxAttr >= 0) ? (uvSetMaxAttr + 1) : uvPools.Count;

            // faces
            if (!TryParsePolyFaces(out var faces, out var maxUvSetIndexInFaces, out var anyHoles, out var holeFaceCount, out var anyFaceVaryingNormalIds))
            {
                // faces無し: points/empty topologyで保持
                var pointsMesh = BuildPointsOnlyMesh(vertsMaya, options);
                ApplyUnityMeshToTarget(targetGO, pointsMesh);

                ExpandedToOriginalVertex = BuildIdentityMap(vertsMaya.Length);

                BuiltMesh = true;
                HadFaces = false;
                HasFaceHoles = false;
                HoleFaceCount = 0;
                UsedFaceVaryingNormals = false;

                BuildNote = "No faces found (expected fc[...]). Built points-only Mesh to preserve geometry data.";
                return;
            }

            HadFaces = true;
            HasFaceHoles = anyHoles;
            HoleFaceCount = holeFaceCount;

            // if edges exist, rebuild vertex loop from edge refs when available
            bool usedEdgeBasedFaces = false;
            if (edges != null && edges.Length > 0)
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    if (faces[i]?.EdgeRefs != null && faces[i].EdgeRefs.Count >= 3)
                    {
                        faces[i].V = BuildVertexLoopFromEdgeRefs(faces[i].EdgeRefs, edges);
                        usedEdgeBasedFaces = true;
                    }
                }
            }

            // fallback: if V missing, use EdgeRefs as vertex loop (best-effort)
            if (!usedEdgeBasedFaces)
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    if (faces[i] == null) continue;
                    if (faces[i].V == null && faces[i].EdgeRefs != null)
                    {
                        faces[i].V = new List<int>(faces[i].EdgeRefs.Count);
                        for (int k = 0; k < faces[i].EdgeRefs.Count; k++)
                            faces[i].V.Add(faces[i].EdgeRefs[k]);
                    }
                }
            }

            int uvSetCount = Math.Max(uvPools.Count, maxUvSetIndexInFaces + 1);
            uvSetCount = Mathf.Clamp(uvSetCount, 0, 8); // Unity UV0..UV7

            // face-varying normals supported when normal pool exists and face has NormalIds
            bool hasNormalPool = (normalsPoolMaya != null && normalsPoolMaya.Length > 0);
            UsedFaceVaryingNormals = hasNormalPool && anyFaceVaryingNormalIds;

            // material assignment -> submesh partition
            MayaFaceMaterialAssignments.MeshAssignments assign = null;
            bool hasAssign = MayaFaceMaterialAssignments.TryGetForMesh(scene, NodeName, out assign);

            if (!hasAssign && faces != null && faces.Count > 0)
            {
                if (TryGetFaceAssignFromShadingGroupMetadata(transform.root, NodeName, faces.Count, out var metaAssign))
                {
                    assign = metaAssign;
                    hasAssign = true;
                }
            }

            // convert positions to Unity
            var posUnity = new Vector3[vertsMaya.Length];
            for (int i = 0; i < vertsMaya.Length; i++)
                posUnity[i] = MayaToUnityConversion.ConvertPosition(vertsMaya[i], options.Conversion);

            // ---------------------------------------------------------
            // Normals strategy:
            // 1) face-varying normals: apply per-corner normalId
            // 2) per-vertex normals: normalsPool.Length == verts.Length
            // 3) edge smoothing: use ed[] Hard edges to build smoothing components
            // 4) recalc
            // ---------------------------------------------------------

            bool canUsePerVertexNormals = hasNormalPool && normalsPoolMaya.Length == vertsMaya.Length && !UsedFaceVaryingNormals;

            bool canUseEdgeSmoothing = !UsedFaceVaryingNormals && !canUsePerVertexNormals && edges != null && edges.Length > 0;

            Vector3[] faceNormals = null;
            Dictionary<(int faceIndex, int vertIndex), int> normalGroupByCorner = null;
            Dictionary<(int vertIndex, int groupId), Vector3> normalByVertGroup = null;

            if (canUseEdgeSmoothing)
            {
                // compute per-face normal (Newell) in Unity space
                faceNormals = new Vector3[faces.Count];
                for (int fi = 0; fi < faces.Count; fi++)
                {
                    var fv = faces[fi]?.V;
                    if (fv == null || fv.Count < 3)
                    {
                        faceNormals[fi] = Vector3.up;
                        continue;
                    }

                    faceNormals[fi] = ComputePolygonNormalNewell(fv, posUnity);
                    if (faceNormals[fi].sqrMagnitude < 1e-12f) faceNormals[fi] = Vector3.up;
                    else faceNormals[fi].Normalize();
                }

                // edgeId -> faces
                var edgeToFaces = new Dictionary<int, List<int>>();
                for (int fi = 0; fi < faces.Count; fi++)
                {
                    var er = faces[fi]?.EdgeRefs;
                    if (er == null) continue;

                    for (int ei = 0; ei < er.Count; ei++)
                    {
                        int edgeId = DecodeEdgeId(er[ei]);
                        if (edgeId < 0 || edgeId >= edges.Length) continue;

                        if (!edgeToFaces.TryGetValue(edgeId, out var list))
                        {
                            list = new List<int>(2);
                            edgeToFaces[edgeId] = list;
                        }
                        if (!list.Contains(fi)) list.Add(fi);
                    }
                }

                // vert -> incident faces
                var vertToFaces = new Dictionary<int, List<int>>();
                for (int fi = 0; fi < faces.Count; fi++)
                {
                    var fv = faces[fi]?.V;
                    if (fv == null) continue;

                    for (int k = 0; k < fv.Count; k++)
                    {
                        int v = fv[k];
                        if (v < 0 || v >= vertsMaya.Length) continue;

                        if (!vertToFaces.TryGetValue(v, out var list))
                        {
                            list = new List<int>(8);
                            vertToFaces[v] = list;
                        }
                        list.Add(fi);
                    }
                }

                normalGroupByCorner = new Dictionary<(int faceIndex, int vertIndex), int>();
                normalByVertGroup = new Dictionary<(int vertIndex, int groupId), Vector3>();

                foreach (var kv in vertToFaces)
                {
                    int v = kv.Key;
                    var incidentFaces = kv.Value;
                    if (incidentFaces == null || incidentFaces.Count == 0) continue;

                    var visited = new HashSet<int>();
                    int groupId = 0;

                    for (int idx = 0; idx < incidentFaces.Count; idx++)
                    {
                        int startFace = incidentFaces[idx];
                        if (visited.Contains(startFace)) continue;

                        var q = new Queue<int>();
                        var componentFaces = new List<int>(8);

                        visited.Add(startFace);
                        q.Enqueue(startFace);

                        while (q.Count > 0)
                        {
                            int f = q.Dequeue();
                            componentFaces.Add(f);

                            var edgeRefs = faces[f]?.EdgeRefs;
                            if (edgeRefs == null) continue;

                            for (int ei = 0; ei < edgeRefs.Count; ei++)
                            {
                                int er = edgeRefs[ei];
                                int edgeId = DecodeEdgeId(er);
                                if (edgeId < 0 || edgeId >= edges.Length) continue;

                                if (edges[edgeId].Hard) continue;

                                if (!edgeToFaces.TryGetValue(edgeId, out var neighFaces)) continue;

                                for (int ni = 0; ni < neighFaces.Count; ni++)
                                {
                                    int nf = neighFaces[ni];
                                    if (nf == f) continue;
                                    if (visited.Contains(nf)) continue;

                                    if (!FaceContainsVertex(faces[nf]?.V, v)) continue;

                                    visited.Add(nf);
                                    q.Enqueue(nf);
                                }
                            }
                        }

                        Vector3 avg = Vector3.zero;
                        for (int i = 0; i < componentFaces.Count; i++)
                            avg += faceNormals[componentFaces[i]];

                        if (avg.sqrMagnitude < 1e-12f) avg = Vector3.up;
                        else avg.Normalize();

                        normalByVertGroup[(v, groupId)] = avg;

                        for (int i = 0; i < componentFaces.Count; i++)
                            normalGroupByCorner[(componentFaces[i], v)] = groupId;

                        groupId++;
                    }
                }
            }

            // ---------------------------------------------------------
            // Expand vertices per corner based on UV ids + normal id + smoothing group
            // ---------------------------------------------------------

            var newVerts = new List<Vector3>(vertsMaya.Length);
            var newNormals = new List<Vector3>(vertsMaya.Length);
            var newColors = canUseVertexColors ? new List<Color>(vertsMaya.Length) : null;

            var newUvSets = new List<Vector2>[uvSetCount];
            for (int i = 0; i < uvSetCount; i++)
                newUvSets[i] = new List<Vector2>(vertsMaya.Length);

            var expandedToOrig = new List<int>(vertsMaya.Length);

            var vertMap = new Dictionary<VertKey, int>(VertKeyComparer.Instance);
            int[] uvIdsBuffer = new int[Math.Max(uvSetCount, 1)];

            int GetOrCreate(int faceIndex, int cornerIndex, int origV, FaceData face)
            {
                int ng = 0;
                if (canUseEdgeSmoothing && normalGroupByCorner != null)
                {
                    if (!normalGroupByCorner.TryGetValue((faceIndex, origV), out ng))
                        ng = 0;
                }

                FillUvIds(face, cornerIndex, uvIdsBuffer, uvSetCount);

                int normalId = -1;
                if (UsedFaceVaryingNormals && face != null && face.NormalIds != null && cornerIndex < face.NormalIds.Count)
                    normalId = face.NormalIds[cornerIndex];

                var key = new VertKey(
                    origV,
                    ng,
                    normalId,
                    uvSetCount > 0 ? uvIdsBuffer[0] : -1,
                    uvSetCount > 1 ? uvIdsBuffer[1] : -1,
                    uvSetCount > 2 ? uvIdsBuffer[2] : -1,
                    uvSetCount > 3 ? uvIdsBuffer[3] : -1,
                    uvSetCount > 4 ? uvIdsBuffer[4] : -1,
                    uvSetCount > 5 ? uvIdsBuffer[5] : -1,
                    uvSetCount > 6 ? uvIdsBuffer[6] : -1,
                    uvSetCount > 7 ? uvIdsBuffer[7] : -1);

                if (vertMap.TryGetValue(key, out var idx))
                    return idx;

                idx = newVerts.Count;
                vertMap[key] = idx;

                newVerts.Add(posUnity[origV]);
                expandedToOrig.Add(origV);

                // normals
                Vector3 nrm = Vector3.zero;

                if (UsedFaceVaryingNormals && hasNormalPool)
                {
                    if (normalId >= 0 && normalId < normalsPoolMaya.Length)
                    {
                        nrm = MayaToUnityConversion.ConvertDirection(normalsPoolMaya[normalId], options.Conversion);
                        if (nrm.sqrMagnitude > 1e-12f) nrm.Normalize();
                    }
                }
                else if (canUsePerVertexNormals && hasNormalPool)
                {
                    nrm = MayaToUnityConversion.ConvertDirection(normalsPoolMaya[origV], options.Conversion);
                    if (nrm.sqrMagnitude > 1e-12f) nrm.Normalize();
                }
                else if (canUseEdgeSmoothing && normalByVertGroup != null)
                {
                    if (!normalByVertGroup.TryGetValue((origV, ng), out nrm))
                        nrm = Vector3.zero;
                }

                newNormals.Add(nrm);

                // colors
                if (newColors != null)
                    newColors.Add(colorsPool[origV]);

                // UVs
                for (int si = 0; si < uvSetCount; si++)
                {
                    Vector2 uv = Vector2.zero;
                    if (uvPools.TryGetValue(si, out var pool) && pool != null)
                    {
                        int uvid = uvIdsBuffer[si];
                        if (uvid >= 0 && uvid < pool.Length)
                            uv = pool[uvid];
                    }
                    newUvSets[si].Add(uv);
                }

                return idx;
            }

            // tri indices per face (for material partition)
            var trisPerFace = new List<List<int>>(faces.Count);
            for (int i = 0; i < faces.Count; i++)
                trisPerFace.Add(new List<int>(6));

            for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
            {
                var f = faces[faceIndex];
                if (f == null || f.V == null || f.V.Count < 3) continue;

                // NOTE: holesがあっても外周のみfan triangulation (holesは保持)
                int n = f.V.Count;

                for (int i = 1; i + 1 < n; i++)
                {
                    int v0 = f.V[0];
                    int v1 = f.V[i];
                    int v2 = f.V[i + 1];

                    if (v0 < 0 || v0 >= vertsMaya.Length) continue;
                    if (v1 < 0 || v1 >= vertsMaya.Length) continue;
                    if (v2 < 0 || v2 >= vertsMaya.Length) continue;

                    int a = GetOrCreate(faceIndex, 0, v0, f);
                    int b = GetOrCreate(faceIndex, i, v1, f);
                    int c = GetOrCreate(faceIndex, i + 1, v2, f);

                    trisPerFace[faceIndex].Add(a);
                    trisPerFace[faceIndex].Add(b);
                    trisPerFace[faceIndex].Add(c);
                }
            }

            int totalTriIdx = 0;
            for (int i = 0; i < trisPerFace.Count; i++) totalTriIdx += trisPerFace[i].Count;

            if (newVerts.Count == 0)
            {
                var empty = new Mesh { name = string.IsNullOrEmpty(NodeName) ? "MayaMesh_Empty" : (NodeName + "_Empty") };
                ApplyUnityMeshToTarget(targetGO, empty);
                ExpandedToOriginalVertex = BuildIdentityMap(vertsMaya.Length);

                BuiltMesh = true;
                BuildNote = "Faces parsed but produced no vertices. Created empty Mesh to preserve node.";
                return;
            }

            // submesh partition
            List<string> submeshKeys;
            List<List<int>> submeshTris;

            if (hasAssign && assign != null && assign.FacesByShadingEngine.Count > 0)
            {
                submeshKeys = new List<string>(assign.FacesByShadingEngine.Count + 1);
                submeshTris = new List<List<int>>(assign.FacesByShadingEngine.Count + 1);

                var assigned = new bool[faces.Count];

                foreach (var kv in assign.FacesByShadingEngine)
                {
                    submeshKeys.Add(kv.Key);

                    var tri = new List<int>(1024);
                    var faceSet = kv.Value;

                    var faceList = new List<int>(faceSet.Count);
                    foreach (var fi in faceSet)
                    {
                        if (fi < 0 || fi >= faces.Count) continue;
                        faceList.Add(fi);
                    }
                    faceList.Sort();

                    for (int i = 0; i < faceList.Count; i++)
                    {
                        int fi = faceList[i];
                        assigned[fi] = true;
                        tri.AddRange(trisPerFace[fi]);
                    }

                    submeshTris.Add(tri);
                }

                var remainder = new List<int>(1024);
                for (int fi = 0; fi < faces.Count; fi++)
                {
                    if (!assigned[fi])
                        remainder.AddRange(trisPerFace[fi]);
                }

                if (remainder.Count > 0)
                {
                    submeshKeys.Add("__Default__");
                    submeshTris.Add(remainder);
                }
            }
            else
            {
                submeshKeys = new List<string> { "__Single__" };
                submeshTris = new List<List<int>> { new List<int>(Math.Max(totalTriIdx, 0)) };
                for (int fi = 0; fi < faces.Count; fi++)
                    submeshTris[0].AddRange(trisPerFace[fi]);
            }

            // build unity mesh
            var mesh = new Mesh { name = string.IsNullOrEmpty(NodeName) ? "MayaMesh" : NodeName };

            if (newVerts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(newVerts);

            for (int si = 0; si < uvSetCount; si++)
                mesh.SetUVs(si, newUvSets[si]);

            if (newColors != null && newColors.Count == newVerts.Count)
                mesh.SetColors(newColors);

            mesh.subMeshCount = submeshTris.Count;
            for (int si = 0; si < submeshTris.Count; si++)
                mesh.SetTriangles(submeshTris[si], si, calculateBounds: false);

            mesh.RecalculateBounds();

            bool anyNormal = false;
            for (int i = 0; i < newNormals.Count; i++)
            {
                if (newNormals[i].sqrMagnitude > 1e-12f) { anyNormal = true; break; }
            }

            if (anyNormal)
            {
                for (int i = 0; i < newNormals.Count; i++)
                {
                    var n = newNormals[i];
                    if (n.sqrMagnitude > 1e-12f) newNormals[i] = n.normalized;
                }
                mesh.SetNormals(newNormals);
            }
            else
            {
                mesh.RecalculateNormals();
            }

            try
            {
                if (uvSetCount > 0)
                    mesh.RecalculateTangents();
            }
            catch { }

            ApplyUnityMeshToTarget(targetGO, mesh);

            // materials
            var mr = targetGO.GetComponent<MeshRenderer>() ?? targetGO.AddComponent<MeshRenderer>();
            var mats = new global::UnityEngine.Material[submeshKeys.Count];
            for (int i = 0; i < submeshKeys.Count; i++)
            {
                var key = submeshKeys[i];

                global::UnityEngine.Material m;
                if (key != "__Single__" && key != "__Default__")
                    m = MayaMaterialResolver.ResolveForShadingEngine(key);
                else
                    m = MayaMaterialResolver.ResolveForMeshNode(NodeName);

                if (m == null)
                    m = new global::UnityEngine.Material(global::UnityEngine.Shader.Find("Standard"));

                mats[i] = m;
            }
            mr.sharedMaterials = mats;

            ExpandedToOriginalVertex = expandedToOrig.ToArray();

            // holes preservation marker component (Unityに概念が無いので明示)
            var holeComp = GetComponent<MayaMeshHolesComponent>() ?? gameObject.AddComponent<MayaMeshHolesComponent>();
            holeComp.HasHoles = HasFaceHoles;
            holeComp.HoleFaceCount = HoleFaceCount;

            BuiltMesh = true;
            BuildNote =
                $"Built mesh: vtx={newVerts.Count}, submeshes={submeshKeys.Count}, uvSets={uvSetCount}, " +
                $"colors={(canUseVertexColors ? "clr[]" : "none")}, " +
                $"normals={(UsedFaceVaryingNormals ? "faceVarying(mn/mf)" : (canUsePerVertexNormals ? "perVertex(n[])" : (canUseEdgeSmoothing ? "edge-smooth(ed[])" : "recalc")))}, " +
                $"faces={(usedEdgeBasedFaces ? "edgeBased(fc+ed)" : "fan(fc)")}, " +
                $"holes={(HasFaceHoles ? ("yes(" + HoleFaceCount + ")") : "no")}, " +
                $"targetGO={(targetGO != null ? targetGO.name : "(null)")}";
        }

        // =========================================================
        // Target GO helpers
        // =========================================================

        private GameObject ResolveMeshTargetGO()
        {
            // Maya mesh is usually a shape under a transform.
            // Prefer attaching Unity MeshFilter/MeshRenderer to the parent transform if it exists.
            if (transform != null && transform.parent != null)
            {
                var p = transform.parent;
                var pn = p.GetComponent<MayaNodeComponentBase>();
                if (pn != null && (pn.NodeType == "transform" || pn.NodeType == "joint"))
                    return p.gameObject;
            }

            return gameObject;
        }

        private static void ApplyUnityMeshToTarget(GameObject go, Mesh mesh)
        {
            if (go == null) return;

            var mf = go.GetComponent<MeshFilter>() ?? go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            if (go.GetComponent<MeshRenderer>() == null)
                go.AddComponent<MeshRenderer>();
        }

        private Mesh BuildPointsOnlyMesh(Vector3[] vertsMaya, MayaImportOptions options)
        {
            var v = new List<Vector3>(vertsMaya.Length);
            for (int i = 0; i < vertsMaya.Length; i++)
                v.Add(MayaToUnityConversion.ConvertPosition(vertsMaya[i], options.Conversion));

            var m = new Mesh { name = string.IsNullOrEmpty(NodeName) ? "MayaMesh_Points" : (NodeName + "_Points") };
            if (v.Count > 65535)
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            m.SetVertices(v);

            var idx = new int[v.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;

            m.subMeshCount = 1;
            m.SetIndices(idx, MeshTopology.Points, 0, calculateBounds: true);
            return m;
        }

        private static int[] BuildIdentityMap(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            return a;
        }

        // =========================================================
        // Scene record helper (.mb)
        // =========================================================

        private static NodeRecord FindNodeRecordByAnyName(MayaSceneData scene, string nameOrLeaf)
        {
            if (scene == null || scene.Nodes == null || string.IsNullOrEmpty(nameOrLeaf))
                return null;

            if (scene.Nodes.TryGetValue(nameOrLeaf, out var exact))
                return exact;

            var leaf = MayaPlugUtil.LeafName(nameOrLeaf);
            if (!string.IsNullOrEmpty(leaf) && scene.Nodes.TryGetValue(leaf, out var leafRec))
                return leafRec;

            // fallback: scan by leaf
            foreach (var kv in scene.Nodes)
            {
                var n = kv.Value;
                if (n == null) continue;
                if (MayaPlugUtil.NodeMatches(n.Name, nameOrLeaf)) return n;
                if (!string.IsNullOrEmpty(leaf) && MayaPlugUtil.LeafName(n.Name) == leaf) return n;
            }

            return null;
        }

        // =========================================================
        // Shading group metadata fallback
        // =========================================================

        private static bool TryGetFaceAssignFromShadingGroupMetadata(Transform root, string meshName, int faceCount, out MayaFaceMaterialAssignments.MeshAssignments assign)
        {
            assign = null;
            if (root == null || string.IsNullOrEmpty(meshName) || faceCount <= 0) return false;

            // This helper exists in project; keep compatibility with existing metadata pipeline.
            return MayaFaceMaterialAssignmentsFromMetadata.TryBuildAssignmentsFromScene(root, meshName, faceCount, out assign);
        }

        // =========================================================
        // Attribute collectors (from SerializedAttribute tokens)
        // =========================================================

        private bool TryCollectFloat3Array(string prefixWithBracket, out Vector3[] arr)
        {
            arr = null;
            if (Attributes == null || Attributes.Count == 0) return false;

            var chunks = new List<(int start, int end, List<string> tokens)>();

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                var key = a.Key;
                if (!(key.StartsWith(prefixWithBracket, StringComparison.Ordinal) || key.StartsWith("." + prefixWithBracket, StringComparison.Ordinal)))
                    continue;

                if (a.Tokens == null || a.Tokens.Count == 0) continue;
                if (!TryParseRange(key, out var start, out var end)) continue;

                chunks.Add((start, end, a.Tokens));
            }

            if (chunks.Count == 0) return false;

            int maxIndex = -1;
            for (int i = 0; i < chunks.Count; i++)
                maxIndex = Math.Max(maxIndex, chunks[i].end);

            if (maxIndex < 0) return false;

            var outArr = new Vector3[maxIndex + 1];

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var (start, end, tokens) = chunks[ci];

                int expectedMin = (end - start + 1) * 3;
                if (tokens.Count < 3) continue;
                if (tokens.Count < expectedMin)
                {
                    // rangeが壊れていても読めるだけ読む
                }

                int t = 0;
                for (int vi = start; vi <= end; vi++)
                {
                    if (t + 2 >= tokens.Count) break;

                    if (!TryF(tokens[t++], out var x)) break;
                    if (!TryF(tokens[t++], out var y)) break;
                    if (!TryF(tokens[t++], out var z)) break;

                    if (vi < 0 || vi >= outArr.Length) continue;
                    outArr[vi] = new Vector3(x, y, z);
                }
            }

            arr = outArr;
            return true;
        }

        private bool TryCollectFloat4Array(string prefixWithBracket, out Color[] arr)
        {
            arr = null;
            if (Attributes == null || Attributes.Count == 0) return false;

            var chunks = new List<(int start, int end, List<string> tokens)>();

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                var key = a.Key;
                if (!(key.StartsWith(prefixWithBracket, StringComparison.Ordinal) || key.StartsWith("." + prefixWithBracket, StringComparison.Ordinal)))
                    continue;

                if (a.Tokens == null || a.Tokens.Count == 0) continue;
                if (!TryParseRange(key, out var start, out var end)) continue;

                chunks.Add((start, end, a.Tokens));
            }

            if (chunks.Count == 0) return false;

            int maxIndex = -1;
            for (int i = 0; i < chunks.Count; i++)
                maxIndex = Math.Max(maxIndex, chunks[i].end);

            if (maxIndex < 0) return false;

            var outArr = new Color[maxIndex + 1];

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var (start, end, tokens) = chunks[ci];

                int t = 0;
                for (int vi = start; vi <= end; vi++)
                {
                    if (t + 3 >= tokens.Count) break;

                    if (!TryF(tokens[t++], out var r)) break;
                    if (!TryF(tokens[t++], out var g)) break;
                    if (!TryF(tokens[t++], out var b)) break;
                    if (!TryF(tokens[t++], out var a)) break;

                    if (vi < 0 || vi >= outArr.Length) continue;
                    outArr[vi] = new Color(r, g, b, a);
                }
            }

            arr = outArr;
            return true;
        }

        private bool TryCollectEdges(out Edge[] edges)
        {
            edges = null;
            if (Attributes == null || Attributes.Count == 0) return false;

            var chunks = new List<(int start, int end, List<string> tokens)>();

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                var key = a.Key;
                if (!(key.StartsWith("ed[", StringComparison.Ordinal) || key.StartsWith(".ed[", StringComparison.Ordinal)))
                    continue;

                if (a.Tokens == null || a.Tokens.Count == 0) continue;
                if (!TryParseRange(key, out var start, out var end)) continue;

                chunks.Add((start, end, a.Tokens));
            }

            if (chunks.Count == 0) return false;

            int maxIndex = -1;
            for (int i = 0; i < chunks.Count; i++)
                maxIndex = Math.Max(maxIndex, chunks[i].end);

            if (maxIndex < 0) return false;

            var arr = new Edge[maxIndex + 1];

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var (start, end, tokens) = chunks[ci];

                int t = 0;
                for (int ei = start; ei <= end; ei++)
                {
                    if (t + 2 >= tokens.Count) break;

                    if (!TryI(tokens[t++], out var a0)) break;
                    if (!TryI(tokens[t++], out var b0)) break;
                    if (!TryI(tokens[t++], out var hardInt)) break;

                    if (ei < 0 || ei >= arr.Length) continue;

                    arr[ei] = new Edge(a0, b0, hardInt != 0);
                }
            }

            edges = arr;
            return true;
        }

        private Dictionary<int, Vector2[]> CollectUvPools(out int maxSetIndex)
        {
            maxSetIndex = -1;
            var pools = new Dictionary<int, Vector2[]>();
            var chunksBySet = new Dictionary<int, List<(int start, int end, List<string> tokens)>>();

            if (Attributes == null) return pools;

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                var key = a.Key;

                int uvst = key.IndexOf("uvst[", StringComparison.Ordinal);
                if (uvst < 0) uvst = key.IndexOf(".uvst[", StringComparison.Ordinal);
                if (uvst < 0) continue;

                int rb = key.IndexOf(']', uvst);
                if (rb < 0) continue;

                var setIdxStr = key.Substring(uvst + "uvst[".Length, rb - (uvst + "uvst[".Length));
                if (!TryI(setIdxStr, out var setIndex)) continue;

                int uvsp = key.IndexOf("uvsp[", StringComparison.Ordinal);
                if (uvsp < 0) uvsp = key.IndexOf(".uvsp[", StringComparison.Ordinal);
                if (uvsp < 0) continue;

                if (!TryParseRange(key.Substring(uvsp), out var start, out var end)) continue;
                if (a.Tokens == null || a.Tokens.Count < 2) continue;

                if (!chunksBySet.TryGetValue(setIndex, out var list))
                {
                    list = new List<(int start, int end, List<string> tokens)>();
                    chunksBySet[setIndex] = list;
                }

                list.Add((start, end, a.Tokens));
                if (setIndex > maxSetIndex) maxSetIndex = setIndex;
            }

            foreach (var kv in chunksBySet)
            {
                int setIndex = kv.Key;
                var chunks = kv.Value;
                if (chunks == null || chunks.Count == 0) continue;

                int maxIndex = -1;
                for (int i = 0; i < chunks.Count; i++)
                    maxIndex = Math.Max(maxIndex, chunks[i].end);

                if (maxIndex < 0) continue;

                var arr = new Vector2[maxIndex + 1];

                for (int ci = 0; ci < chunks.Count; ci++)
                {
                    var (start, end, tokens) = chunks[ci];

                    int t = 0;
                    for (int uvi = start; uvi <= end; uvi++)
                    {
                        if (t + 1 >= tokens.Count) break;

                        if (!TryF(tokens[t++], out var u)) break;
                        if (!TryF(tokens[t++], out var v)) break;

                        if (uvi < 0 || uvi >= arr.Length) continue;
                        arr[uvi] = new Vector2(u, v);
                    }
                }

                pools[setIndex] = arr;
            }

            return pools;
        }

        // =========================================================
        // polyFaces parsing (fc[...])
        // =========================================================

        private sealed class FaceData
        {
            public List<int> V;
            public Dictionary<int, List<int>> UvIdsPerSet;
            public List<int> EdgeRefs;
            public List<int> NormalIds; // mn/mf best-effort
            public bool HasHoles;
        }

        private bool TryParsePolyFaces(
            out List<FaceData> faces,
            out int maxUvSetIndex,
            out bool anyHoles,
            out int holeFaceCount,
            out bool anyFaceVaryingNormalIds)
        {
            faces = null;
            maxUvSetIndex = -1;
            anyHoles = false;
            holeFaceCount = 0;
            anyFaceVaryingNormalIds = false;

            if (Attributes == null) return false;

            var chunks = new List<(int start, int end, List<string> tokens)>();

            for (int i = 0; i < Attributes.Count; i++)
            {
                var a = Attributes[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                var key = a.Key;
                if (!(key.StartsWith("fc[", StringComparison.Ordinal) || key.StartsWith(".fc[", StringComparison.Ordinal)))
                    continue;

                if (a.Tokens == null || a.Tokens.Count == 0) continue;
                if (!TryParseRange(key, out var start, out var end)) continue;

                chunks.Add((start, end, a.Tokens));
            }

            if (chunks.Count == 0) return false;

            int maxIndex = -1;
            for (int i = 0; i < chunks.Count; i++)
                maxIndex = Math.Max(maxIndex, chunks[i].end);

            if (maxIndex < 0) return false;

            var arr = new FaceData[maxIndex + 1];

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var (start, end, tokens) = chunks[ci];

                int t = 0;

                for (int fi = start; fi <= end; fi++)
                {
                    var face = new FaceData();

                    // f <n> <v...>
                    if (t < tokens.Count && tokens[t] == "f")
                    {
                        t++;
                        if (t >= tokens.Count) break;
                        if (!TryI(tokens[t++], out var n)) break;

                        face.V = new List<int>(Mathf.Max(3, n));
                        for (int k = 0; k < n && t < tokens.Count; k++)
                        {
                            if (!TryI(tokens[t++], out var v)) break;
                            face.V.Add(v);
                        }
                    }

                    // tags until next f
                    while (t < tokens.Count)
                    {
                        var tag = tokens[t];
                        if (tag == "f") break;
                        t++;

                        if (tag == "mu")
                        {
                            // mu <setIdx> <n> <uvid...>
                            if (t + 1 >= tokens.Count) break;
                            if (!TryI(tokens[t++], out var setIdx)) break;
                            if (!TryI(tokens[t++], out var n)) break;

                            face.UvIdsPerSet ??= new Dictionary<int, List<int>>();

                            var list = new List<int>(Mathf.Max(3, n));
                            for (int k = 0; k < n && t < tokens.Count; k++)
                            {
                                if (!TryI(tokens[t++], out var uvid)) break;
                                list.Add(uvid);
                            }

                            face.UvIdsPerSet[setIdx] = list;
                            if (setIdx > maxUvSetIndex) maxUvSetIndex = setIdx;
                        }
                        else if (tag == "ed")
                        {
                            // ed <n> <edgeRef...>
                            if (t >= tokens.Count) break;
                            if (!TryI(tokens[t++], out var n)) break;

                            face.EdgeRefs = new List<int>(Mathf.Max(3, n));
                            for (int k = 0; k < n && t < tokens.Count; k++)
                            {
                                if (!TryI(tokens[t++], out var e)) break;
                                face.EdgeRefs.Add(e);
                            }
                        }
                        else if (tag == "mn" || tag == "mf")
                        {
                            // best-effort: <n> <normalId...>
                            if (t >= tokens.Count) break;
                            if (!TryI(tokens[t++], out var n)) break;

                            face.NormalIds ??= new List<int>(Mathf.Max(3, n));
                            for (int k = 0; k < n && t < tokens.Count; k++)
                            {
                                if (!TryI(tokens[t++], out var nid)) break;
                                face.NormalIds.Add(nid);
                            }

                            if (face.NormalIds.Count > 0)
                                anyFaceVaryingNormalIds = true;
                        }
                        else if (tag == "h")
                        {
                            // holes: mark + best-effort skip to keep stream aligned
                            face.HasHoles = true;
                            anyHoles = true;
                            holeFaceCount++;

                            if (t < tokens.Count && TryI(tokens[t], out var holeCount))
                            {
                                t++;
                                for (int hi = 0; hi < holeCount && t < tokens.Count; hi++)
                                {
                                    if (!TryI(tokens[t++], out var m)) break;
                                    for (int k = 0; k < m && t < tokens.Count; k++)
                                    {
                                        if (!TryI(tokens[t++], out _)) break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Unknown: best-effort skip count pattern
                            if (t < tokens.Count && TryI(tokens[t], out var n))
                            {
                                t++;
                                t = Math.Min(t + n, tokens.Count);
                            }
                            else
                            {
                                // cannot safely skip; stop
                                break;
                            }
                        }
                    }

                    arr[fi] = face;
                }
            }

            faces = new List<FaceData>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
                faces.Add(arr[i]);

            return true;
        }

        private static void FillUvIds(FaceData f, int cornerIndex, int[] buffer, int uvSetCount)
        {
            for (int i = 0; i < uvSetCount; i++) buffer[i] = -1;

            if (f == null || f.UvIdsPerSet == null) return;

            for (int si = 0; si < uvSetCount; si++)
            {
                if (!f.UvIdsPerSet.TryGetValue(si, out var list) || list == null || list.Count == 0)
                    continue;

                int idx = Mathf.Clamp(cornerIndex, 0, list.Count - 1);
                buffer[si] = list[idx];
            }
        }

        // =========================================================
        // Geometry helpers
        // =========================================================

        private readonly struct Edge
        {
            public readonly int A;
            public readonly int B;
            public readonly bool Hard;

            public Edge(int a, int b, bool hard)
            {
                A = a;
                B = b;
                Hard = hard;
            }
        }

        private static int DecodeEdgeId(int edgeRef) => edgeRef >= 0 ? edgeRef : (-edgeRef - 1);

        private static List<int> BuildVertexLoopFromEdgeRefs(List<int> edgeRefs, Edge[] edges)
        {
            var loop = new List<int>(edgeRefs.Count);
            if (edgeRefs == null || edgeRefs.Count == 0 || edges == null || edges.Length == 0)
                return loop;

            int first = edgeRefs[0];
            int firstId = DecodeEdgeId(first);
            bool firstFlip = first < 0;

            if (firstId < 0 || firstId >= edges.Length)
                return loop;

            var e0 = edges[firstId];
            int a0 = firstFlip ? e0.B : e0.A;
            int b0 = firstFlip ? e0.A : e0.B;

            loop.Add(a0);
            loop.Add(b0);

            int current = b0;

            for (int i = 1; i < edgeRefs.Count; i++)
            {
                int er = edgeRefs[i];
                int id = DecodeEdgeId(er);
                bool flip = er < 0;
                if (id < 0 || id >= edges.Length) continue;

                var e = edges[id];
                int a = flip ? e.B : e.A;
                int b = flip ? e.A : e.B;

                if (a == current)
                {
                    loop.Add(b);
                    current = b;
                }
                else if (b == current)
                {
                    loop.Add(a);
                    current = a;
                }
                else
                {
                    loop.Add(b);
                    current = b;
                }
            }

            return loop;
        }

        private static bool FaceContainsVertex(List<int> faceVerts, int v)
        {
            if (faceVerts == null) return false;
            for (int i = 0; i < faceVerts.Count; i++)
                if (faceVerts[i] == v) return true;
            return false;
        }

        private static Vector3 ComputePolygonNormalNewell(List<int> verts, Vector3[] pos)
        {
            Vector3 n = Vector3.zero;
            int count = verts.Count;

            for (int i = 0; i < count; i++)
            {
                int i0 = verts[i];
                int i1 = verts[(i + 1) % count];

                var p0 = pos[i0];
                var p1 = pos[i1];

                n.x += (p0.y - p1.y) * (p0.z + p1.z);
                n.y += (p0.z - p1.z) * (p0.x + p1.x);
                n.z += (p0.x - p1.x) * (p0.y + p1.y);
            }

            return n;
        }

        // =========================================================
        // Token parsing helpers
        // =========================================================

        private static bool TryParseRange(string key, out int start, out int end)
        {
            start = 0;
            end = -1;

            int lb = key.IndexOf('[');
            int rb = key.IndexOf(']');
            if (lb < 0 || rb < 0 || rb <= lb + 1)
                return false;

            var inside = key.Substring(lb + 1, rb - lb - 1);
            int colon = inside.IndexOf(':');
            if (colon >= 0)
            {
                var a = inside.Substring(0, colon);
                var b = inside.Substring(colon + 1);

                if (!TryI(a, out start)) return false;
                if (!TryI(b, out end)) return false;
                return true;
            }

            if (!TryI(inside, out start)) return false;
            end = start;
            return true;
        }

        private static bool TryI(string s, out int v)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

        private static bool TryF(string s, out float v)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        // =========================================================
        // Expanded vertex key (UV0..UV7 + face-varying normal + smoothing group)
        // =========================================================

        private readonly struct VertKey
        {
            public readonly int V;
            public readonly int SmoothGroup;
            public readonly int NormalId;
            public readonly int UV0, UV1, UV2, UV3, UV4, UV5, UV6, UV7;

            public VertKey(int v, int smoothGroup, int normalId,
                int uv0, int uv1, int uv2, int uv3, int uv4, int uv5, int uv6, int uv7)
            {
                V = v;
                SmoothGroup = smoothGroup;
                NormalId = normalId;
                UV0 = uv0; UV1 = uv1; UV2 = uv2; UV3 = uv3;
                UV4 = uv4; UV5 = uv5; UV6 = uv6; UV7 = uv7;
            }
        }

        private sealed class VertKeyComparer : IEqualityComparer<VertKey>
        {
            public static readonly VertKeyComparer Instance = new VertKeyComparer();

            public bool Equals(VertKey a, VertKey b)
            {
                return a.V == b.V &&
                       a.SmoothGroup == b.SmoothGroup &&
                       a.NormalId == b.NormalId &&
                       a.UV0 == b.UV0 && a.UV1 == b.UV1 && a.UV2 == b.UV2 && a.UV3 == b.UV3 &&
                       a.UV4 == b.UV4 && a.UV5 == b.UV5 && a.UV6 == b.UV6 && a.UV7 == b.UV7;
            }

            public int GetHashCode(VertKey k)
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + k.V;
                    h = h * 31 + k.SmoothGroup;
                    h = h * 31 + k.NormalId;
                    h = h * 31 + k.UV0; h = h * 31 + k.UV1; h = h * 31 + k.UV2; h = h * 31 + k.UV3;
                    h = h * 31 + k.UV4; h = h * 31 + k.UV5; h = h * 31 + k.UV6; h = h * 31 + k.UV7;
                    return h;
                }
            }
        }
    }
}

namespace MayaImporter.Components
{
    /// <summary>
    /// Unity Mesh に「穴」という概念が無いため、meshノードが hole token を持っていた事実を保持するためのコンポーネント。
    /// Raw詳細は MayaNodeComponentBase.Attributes に lossless token として保持される。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaMeshHolesComponent : MonoBehaviour
    {
        public bool HasHoles;
        public int HoleFaceCount;
    }
}
