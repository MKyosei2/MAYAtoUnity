#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    /// <summary>
    /// Replace MayaUnknownNodeComponent / MayaPlaceholderNode in CURRENT scene
    /// with the resolved mapped component type from NodeFactory (if exists).
    ///
    /// - Does NOT reimport .ma/.mb
    /// - Copies identity/attributes/connections
    /// - Keeps the same GameObject (so hierarchy/refs stay)
    /// </summary>
    public static class MayaUnknownNodeUpgrader
    {
        private const string MenuPath = "Tools/Maya Importer/Coverage/Upgrade Unknown Nodes In CURRENT Scene";

        [MenuItem(MenuPath)]
        public static void Upgrade()
        {
            int upgraded = 0;
            int skippedNoMapping = 0;
            int skippedAlreadyHas = 0;

            // Include inactive, but only valid scene objects (exclude prefabs/assets)
            var unknowns = UnityEngine.Object.FindObjectsByType<MayaUnknownNodeComponent>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            var placeholders = UnityEngine.Object.FindObjectsByType<MayaPlaceholderNode>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            // Process unknowns
            for (int i = 0; i < unknowns.Length; i++)
            {
                var src = unknowns[i];
                if (src == null) continue;
                if (!src.gameObject.scene.IsValid()) continue;

                if (TryUpgradeOne(src, out var result))
                {
                    upgraded += result.upgraded;
                    skippedNoMapping += result.skippedNoMapping;
                    skippedAlreadyHas += result.skippedAlreadyHas;
                }
            }

            // Process placeholders
            for (int i = 0; i < placeholders.Length; i++)
            {
                var src = placeholders[i];
                if (src == null) continue;
                if (!src.gameObject.scene.IsValid()) continue;

                if (TryUpgradeOne(src, out var result))
                {
                    upgraded += result.upgraded;
                    skippedNoMapping += result.skippedNoMapping;
                    skippedAlreadyHas += result.skippedAlreadyHas;
                }
            }

            if (upgraded > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[MayaImporter] Upgrade Unknown Nodes: upgraded={upgraded}, skipped(no mapping)={skippedNoMapping}, skipped(already has mapped type)={skippedAlreadyHas}");
            EditorUtility.DisplayDialog(
                "MayaImporter",
                $"Upgrade Unknown Nodes 完了\n\nupgraded = {upgraded}\nno mapping = {skippedNoMapping}\nalready has = {skippedAlreadyHas}\n\n必要なら再度 Frequency/ Coverage を実行して残りを確認してください。",
                "OK"
            );
        }

        private struct UpgradeResult
        {
            public int upgraded;
            public int skippedNoMapping;
            public int skippedAlreadyHas;
        }

        private static bool TryUpgradeOne(MayaNodeComponentBase src, out UpgradeResult result)
        {
            result = default;
            if (src == null) return false;

            var nodeType = src.NodeType;
            if (string.IsNullOrEmpty(nodeType))
            {
                result.skippedNoMapping++;
                return true;
            }

            var mappedType = NodeFactory.ResolveType(nodeType);
            if (mappedType == null)
            {
                result.skippedNoMapping++;
                return true;
            }

            // If mapping still points to unknown/placeholder, nothing to do
            if (mappedType == typeof(MayaUnknownNodeComponent) || mappedType == typeof(MayaPlaceholderNode))
            {
                result.skippedNoMapping++;
                return true;
            }

            // If already has the mapped type, remove unknown/placeholder only
            var go = src.gameObject;
            if (go.GetComponent(mappedType) != null)
            {
                Undo.RegisterCompleteObjectUndo(go, "Remove Unknown Node Component");
                Undo.DestroyObjectImmediate(src);
                result.skippedAlreadyHas++;
                return true;
            }

            // Add mapped component
            Undo.RegisterCompleteObjectUndo(go, "Upgrade Maya Node Component");
            var dst = Undo.AddComponent(go, mappedType) as MayaNodeComponentBase;
            if (dst == null)
            {
                // Failed to add or wrong base type; keep src
                result.skippedNoMapping++;
                return true;
            }

            // Copy base data (deep copy lists)
            dst.NodeName = src.NodeName;
            dst.NodeType = src.NodeType;
            dst.ParentName = src.ParentName;
            dst.Uuid = src.Uuid;

            dst.Attributes = DeepCopyAttributes(src.Attributes);
            dst.Connections = DeepCopyConnections(src.Connections);

            // Remove old unknown/placeholder
            Undo.DestroyObjectImmediate(src);

            result.upgraded++;
            return true;
        }

        private static List<MayaNodeComponentBase.SerializedAttribute> DeepCopyAttributes(List<MayaNodeComponentBase.SerializedAttribute> src)
        {
            var dst = new List<MayaNodeComponentBase.SerializedAttribute>(src != null ? src.Count : 0);
            if (src == null) return dst;

            for (int i = 0; i < src.Count; i++)
            {
                var a = src[i];
                if (a == null) continue;

                var na = new MayaNodeComponentBase.SerializedAttribute
                {
                    Key = a.Key,
                    TypeName = a.TypeName,
                    Tokens = new List<string>(a.Tokens != null ? a.Tokens.Count : 0)
                };

                if (a.Tokens != null)
                    na.Tokens.AddRange(a.Tokens);

                dst.Add(na);
            }

            return dst;
        }

        private static List<MayaNodeComponentBase.SerializedConnection> DeepCopyConnections(List<MayaNodeComponentBase.SerializedConnection> src)
        {
            var dst = new List<MayaNodeComponentBase.SerializedConnection>(src != null ? src.Count : 0);
            if (src == null) return dst;

            for (int i = 0; i < src.Count; i++)
            {
                var c = src[i];
                if (c == null) continue;

                dst.Add(new MayaNodeComponentBase.SerializedConnection
                {
                    SrcPlug = c.SrcPlug,
                    DstPlug = c.DstPlug,
                    Force = c.Force,

                    RoleForThisNode = c.RoleForThisNode,
                    SrcNodePart = c.SrcNodePart,
                    DstNodePart = c.DstNodePart
                });
            }

            return dst;
        }
    }
}
#endif
