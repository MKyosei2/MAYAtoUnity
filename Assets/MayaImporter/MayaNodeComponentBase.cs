using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Phase-1:
    /// Every Maya node must be represented by exactly one Unity component on a GameObject.
    /// Stores lossless-ish node identity + raw attributes + related connections.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MayaNodeComponentBase : MonoBehaviour
    {
        [Header("Maya Identity")]
        public string NodeName;
        public string NodeType;
        public string ParentName;
        public string Uuid;

        [Header("Raw Attributes (lossless tokens)")]
        public List<SerializedAttribute> Attributes = new List<SerializedAttribute>();

        [Header("Related Connections (src/dst plugs)")]
        public List<SerializedConnection> Connections = new List<SerializedConnection>();

        [Serializable]
        public sealed class SerializedAttribute
        {
            public string Key;        // attribute path (e.g. "t" / "tx" / "uvst[0].uvsp[3].uvspu")
            public string TypeName;   // Maya -type if present
            public List<string> Tokens = new List<string>(); // value tokens
        }

        public enum ConnectionRole
        {
            Unknown = 0,
            Source = 1,
            Destination = 2,
            Both = 3
        }

        [Serializable]
        public sealed class SerializedConnection
        {
            public string SrcPlug;
            public string DstPlug;
            public bool Force;

            public ConnectionRole RoleForThisNode;
            public string SrcNodePart;
            public string DstNodePart;
        }

        /// <summary>
        /// Called by builder after AddComponent.
        /// Phase-1 Step-2: inject raw attributes + connections robustly (DAG path, namespace, etc).
        /// </summary>
        public virtual void InitializeFromRecord(NodeRecord rec, List<ConnectionRecord> allConnections)
        {
            if (rec == null) return;

            NodeName = rec.Name;
            NodeType = rec.NodeType;
            ParentName = rec.ParentName;
            Uuid = rec.Uuid;

            // ---- Attributes (lossless) ----
            Attributes.Clear();
            if (rec.Attributes != null)
            {
                foreach (var kv in rec.Attributes)
                {
                    var raw = kv.Value;
                    var a = new SerializedAttribute
                    {
                        Key = kv.Key,
                        TypeName = raw != null ? raw.TypeName : null
                    };

                    if (raw != null && raw.ValueTokens != null)
                        a.Tokens.AddRange(raw.ValueTokens);

                    Attributes.Add(a);
                }
            }

            // ---- Connections (robust association) ----
            Connections.Clear();
            if (allConnections == null || string.IsNullOrEmpty(NodeName))
                return;

            for (int i = 0; i < allConnections.Count; i++)
            {
                var c = allConnections[i];
                if (c == null) continue;

                var srcNodePart = MayaPlugUtil.ExtractNodePart(c.SrcPlug);
                var dstNodePart = MayaPlugUtil.ExtractNodePart(c.DstPlug);

                bool isSrc = MayaPlugUtil.NodeMatches(srcNodePart, NodeName);
                bool isDst = MayaPlugUtil.NodeMatches(dstNodePart, NodeName);

                if (!isSrc && !isDst)
                    continue;

                var role = ConnectionRole.Unknown;
                if (isSrc && isDst) role = ConnectionRole.Both;
                else if (isSrc) role = ConnectionRole.Source;
                else if (isDst) role = ConnectionRole.Destination;

                Connections.Add(new SerializedConnection
                {
                    SrcPlug = c.SrcPlug,
                    DstPlug = c.DstPlug,
                    Force = c.Force,
                    RoleForThisNode = role,
                    SrcNodePart = srcNodePart,
                    DstNodePart = dstNodePart
                });
            }
        }

        /// <summary>
        /// Phase-1 Step-3:
        /// Node-specific Unity reconstruction hook (Transform/Camera/Light/etc).
        /// Default: no-op.
        /// </summary>
        public virtual void ApplyToUnity(MayaImportOptions options, MayaImportLog log)
        {
            // default no-op
        }

        /// <summary>
        /// Helper: try get attribute tokens by key (supports "t" and ".t" compatibility).
        /// </summary>
        protected bool TryGetAttr(string key, out SerializedAttribute attr)
        {
            attr = null;
            if (string.IsNullOrEmpty(key)) return false;

            // exact
            for (int i = 0; i < Attributes.Count; i++)
            {
                if (string.Equals(Attributes[i].Key, key, StringComparison.Ordinal))
                {
                    attr = Attributes[i];
                    return true;
                }
            }

            // dot-compat
            var dot = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : "." + key;
            for (int i = 0; i < Attributes.Count; i++)
            {
                if (string.Equals(Attributes[i].Key, dot, StringComparison.Ordinal))
                {
                    attr = Attributes[i];
                    return true;
                }
            }

            return false;
        }
    }
}
