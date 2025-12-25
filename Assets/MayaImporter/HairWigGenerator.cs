using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Hair
{
    /// <summary>
    /// Maya Hair / nHair / XGen 等を Unity 上で「見える化」するための簡易ストランド生成器。
    /// Maya/API無し前提なので、1:1 シミュレーションではなく「再構築できた」ことを保証する目的。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HairWigGenerator : MonoBehaviour
    {
        [Header("Maya Hair Info")]
        public string mayaNodeName;

        [Header("Generation")]
        public bool autoGenerateOnApply = true;
        public float strandLength = 0.12f;
        public float strandWidth = 0.002f;
        public bool followRoots = true;

        [Header("Fallback (when no roots exist)")]
        public int fallbackStrandCount = 50;

        [Header("Generated Objects")]
        public List<GameObject> generatedStrands = new List<GameObject>();

        [Header("Resolved Roots (optional)")]
        public List<Transform> roots = new List<Transform>();

        public void Generate()
        {
            if (roots != null && roots.Count > 0)
            {
                GenerateFromRoots(roots);
                return;
            }

            // Fallback: dummy strands under this object
            Clear();
            int n = Mathf.Max(1, fallbackStrandCount);

            for (int i = 0; i < n; i++)
            {
                var strand = new GameObject($"HairStrand_{i}");
                strand.transform.SetParent(transform, false);

                var line = strand.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = false;

                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, Vector3.down * strandLength);

                line.widthMultiplier = strandWidth;
                generatedStrands.Add(strand);
            }
        }

        public void GenerateFromRoots(IList<Transform> rootList)
        {
            Clear();

            if (rootList == null || rootList.Count == 0)
            {
                Generate();
                return;
            }

            for (int i = 0; i < rootList.Count; i++)
            {
                var r = rootList[i];
                if (r == null) continue;

                var strand = new GameObject($"HairStrand_{i}");
                strand.transform.SetParent(transform, false);

                var line = strand.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.widthMultiplier = strandWidth;

                // 初期
                Vector3 p0 = r.position;
                Vector3 dir = (r.up.sqrMagnitude > 1e-10f) ? r.up.normalized : Vector3.up;
                Vector3 p1 = p0 + dir * strandLength;

                line.SetPosition(0, p0);
                line.SetPosition(1, p1);

                generatedStrands.Add(strand);
            }

            // キャッシュ
            roots.Clear();
            for (int i = 0; i < rootList.Count; i++)
                roots.Add(rootList[i]);
        }

        public void Clear()
        {
            for (int i = 0; i < generatedStrands.Count; i++)
            {
                var strand = generatedStrands[i];
                if (strand != null) DestroyImmediate(strand);
            }
            generatedStrands.Clear();
        }

        private void LateUpdate()
        {
            if (!followRoots) return;
            if (generatedStrands == null || roots == null) return;
            if (generatedStrands.Count == 0 || roots.Count == 0) return;

            int n = Mathf.Min(generatedStrands.Count, roots.Count);
            for (int i = 0; i < n; i++)
            {
                var strand = generatedStrands[i];
                var r = roots[i];
                if (strand == null || r == null) continue;

                var line = strand.GetComponent<LineRenderer>();
                if (line == null || line.positionCount < 2) continue;

                Vector3 p0 = r.position;
                Vector3 dir = (r.up.sqrMagnitude > 1e-10f) ? r.up.normalized : Vector3.up;
                Vector3 p1 = p0 + dir * strandLength;

                line.SetPosition(0, p0);
                line.SetPosition(1, p1);
            }
        }
    }
}
