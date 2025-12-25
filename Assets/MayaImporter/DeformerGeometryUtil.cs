using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Deformers
{
    /// <summary>
    /// Maya の Deformer 情報を Unity 側の Mesh / SkinnedMeshRenderer に
    /// 再適用するための共通ユーティリティ。
    ///
    /// Maya API は一切使用せず、
    /// .ma / .mb から取得した「結果データ」を Unity に反映する役割のみを担う。
    /// </summary>
    public static class DeformerGeometryUtil
    {
        #region Mesh Apply

        /// <summary>
        /// Maya 側で変形済みの頂点結果を Unity Mesh に直接適用する。
        /// （blendShape / nonLinear / lattice 等、結果が bake されている場合用）
        /// </summary>
        public static void ApplyDeformedVertices(
            Mesh mesh,
            Vector3[] vertices,
            Vector3[] normals = null,
            Vector4[] tangents = null)
        {
            if (mesh == null || vertices == null)
            {
                Debug.LogWarning("[DeformerGeometryUtil] Mesh or vertices is null.");
                return;
            }

            if (mesh.vertexCount != vertices.Length)
            {
                Debug.LogWarning(
                    $"[DeformerGeometryUtil] Vertex count mismatch. " +
                    $"Mesh:{mesh.vertexCount}  Data:{vertices.Length}");
                return;
            }

            mesh.vertices = vertices;

            if (normals != null && normals.Length == vertices.Length)
            {
                mesh.normals = normals;
            }
            else
            {
                mesh.RecalculateNormals();
            }

            if (tangents != null && tangents.Length == vertices.Length)
            {
                mesh.tangents = tangents;
            }
            else
            {
                mesh.RecalculateTangents();
            }

            mesh.RecalculateBounds();
        }

        #endregion


        #region SkinCluster (SkinnedMesh)

        /// <summary>
        /// Maya の skinCluster 情報を Unity SkinnedMeshRenderer に適用する。
        /// </summary>
        public static void ApplySkinCluster(
            SkinnedMeshRenderer skinnedMeshRenderer,
            Transform[] bones,
            Matrix4x4[] bindPoses,
            BoneWeight[] boneWeights)
        {
            if (skinnedMeshRenderer == null)
            {
                Debug.LogWarning("[DeformerGeometryUtil] SkinnedMeshRenderer is null.");
                return;
            }

            if (skinnedMeshRenderer.sharedMesh == null)
            {
                Debug.LogWarning("[DeformerGeometryUtil] SharedMesh is null.");
                return;
            }

            if (bones == null || bindPoses == null || boneWeights == null)
            {
                Debug.LogWarning("[DeformerGeometryUtil] Skin data is incomplete.");
                return;
            }

            var mesh = skinnedMeshRenderer.sharedMesh;

            if (mesh.vertexCount != boneWeights.Length)
            {
                Debug.LogWarning(
                    $"[DeformerGeometryUtil] BoneWeight count mismatch. " +
                    $"Mesh:{mesh.vertexCount}  Weights:{boneWeights.Length}");
                return;
            }

            mesh.bindposes = bindPoses;
            mesh.boneWeights = boneWeights;

            skinnedMeshRenderer.bones = bones;
            skinnedMeshRenderer.sharedMesh = mesh;
        }

        #endregion


        #region BlendShape

        /// <summary>
        /// Maya の blendShape を Unity Mesh の BlendShape として再構築する。
        /// </summary>
        public static void ApplyBlendShapes(
            Mesh mesh,
            List<BlendShapeData> blendShapes)
        {
            if (mesh == null || blendShapes == null)
                return;

            foreach (var shape in blendShapes)
            {
                if (shape == null || shape.DeltaVertices == null)
                    continue;

                if (shape.DeltaVertices.Length != mesh.vertexCount)
                {
                    Debug.LogWarning(
                        $"[DeformerGeometryUtil] BlendShape '{shape.Name}' vertex count mismatch.");
                    continue;
                }

                mesh.AddBlendShapeFrame(
                    shape.Name,
                    shape.Weight,
                    shape.DeltaVertices,
                    shape.DeltaNormals,
                    shape.DeltaTangents);
            }
        }

        #endregion


        #region NonLinear / Lattice / Generic Deformer Support

        /// <summary>
        /// Maya 非線形デフォーマ（bend / twist / squash 等）や
        /// lattice / wrap など、Unity に直接対応しない Deformer 用。
        ///
        /// Unity 上では「情報保持コンポーネント」を追加するだけで、
        /// ジオメトリ自体は変更しない。
        /// </summary>
        public static T AttachDeformerComponent<T>(
            GameObject target,
            Dictionary<string, object> attributes)
            where T : Component
        {
            if (target == null)
                return null;

            var component = target.GetComponent<T>();
            if (component == null)
                component = target.AddComponent<T>();

            if (component is IDeformerDataReceiver receiver && attributes != null)
            {
                receiver.ReceiveDeformerAttributes(attributes);
            }

            return component;
        }

        #endregion
    }


    #region Supporting Data Structures

    /// <summary>
    /// Maya BlendShape 1ターゲット分のデータ。
    /// Maya API を使わず、.ma/.mb 解析結果のみで構成される。
    /// </summary>
    public class BlendShapeData
    {
        public string Name;
        public float Weight = 100f;
        public Vector3[] DeltaVertices;
        public Vector3[] DeltaNormals;
        public Vector3[] DeltaTangents;
    }

    /// <summary>
    /// Unity に存在しない Deformer 情報を
    /// Component 側で受け取るためのインターフェース。
    /// </summary>
    public interface IDeformerDataReceiver
    {
        void ReceiveDeformerAttributes(Dictionary<string, object> attributes);
    }

    #endregion
}
