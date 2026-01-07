// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using UnityEngine;

namespace MayaImporter.Geometry
{
    /// <summary>
    /// Aggregates mesh readers.
    /// </summary>
    [DisallowMultipleComponent]
    public class MeshReader : MonoBehaviour
    {
        public MeshVertexReader vertexReader;
        public MeshTopologyReader topologyReader;
        public MeshNormalReader normalReader;
        public MeshTangentReader tangentReader;
        public MeshUVReader uvReader;
        public MeshColorReader colorReader;

        public void CollectFrom(GameObject go)
        {
            vertexReader = go.GetComponent<MeshVertexReader>();
            topologyReader = go.GetComponent<MeshTopologyReader>();
            normalReader = go.GetComponent<MeshNormalReader>();
            tangentReader = go.GetComponent<MeshTangentReader>();
            uvReader = go.GetComponent<MeshUVReader>();
            colorReader = go.GetComponent<MeshColorReader>();
        }
    }
}
