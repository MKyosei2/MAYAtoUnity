// Assets/MayaImporter/MayaOpaqueConnectionPreview.cs
// Inspector-friendly preview of connectAttr (no Maya/API).
// Works for any MayaNodeComponentBase.

using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MayaOpaqueConnectionPreview : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string role;   // Source / Destination / Both / Unknown
            public string srcPlug;
            public string dstPlug;
            public bool force;
        }

        [Header("Counts")]
        public int connectionCount;
        public int incomingCount;
        public int outgoingCount;

        [Tooltip("Max entries kept for inspector preview (keeps last N).")]
        public int maxEntries = 128;

        public List<Entry> entries = new();

        public void BuildFrom(MayaNodeComponentBase node)
        {
            entries.Clear();

            if (node == null || node.Connections == null)
            {
                connectionCount = incomingCount = outgoingCount = 0;
                return;
            }

            connectionCount = node.Connections.Count;

            incomingCount = 0;
            outgoingCount = 0;

            for (int i = 0; i < node.Connections.Count; i++)
            {
                var c = node.Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Destination ||
                    c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Both)
                    incomingCount++;

                if (c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Source ||
                    c.RoleForThisNode == MayaNodeComponentBase.ConnectionRole.Both)
                    outgoingCount++;
            }

            int take = Mathf.Clamp(maxEntries, 0, 4096);
            int n = Mathf.Min(take, node.Connections.Count);
            int start = Mathf.Max(0, node.Connections.Count - n);

            for (int i = start; i < node.Connections.Count; i++)
            {
                var c = node.Connections[i];
                if (c == null) continue;

                entries.Add(new Entry
                {
                    role = c.RoleForThisNode.ToString(),
                    srcPlug = c.SrcPlug ?? "",
                    dstPlug = c.DstPlug ?? "",
                    force = c.Force
                });
            }
        }
    }
}
