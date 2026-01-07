// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System.Collections.Generic;

namespace MayaImporter.Core.Connections
{
    public class ConnectionGraph
    {
        public readonly List<MayaConnection> Connections = new();

        public void Add(MayaConnection conn)
        {
            Connections.Add(conn);
        }
    }
}
