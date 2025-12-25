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
