// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
using System;
using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Inspector-proof component attached to Transform/Joint nodes when TRS is driven by DG connections.
    /// This does NOT evaluate anything by itself; MayaRuntimeGraphEvaluator drives it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaDrivenTransformRuntime : MonoBehaviour
    {
        [Header("Source")]
        public string mayaNodeName;
        public string mayaNodeType;

        [Header("Driven plugs (dest)")]
        public string txPlug;
        public string tyPlug;
        public string tzPlug;
        public string rxPlug;
        public string ryPlug;
        public string rzPlug;
        public string sxPlug;
        public string syPlug;
        public string szPlug;

        [Header("Runtime last values (Maya space)")]
        public Vector3 lastTranslate;
        public Vector3 lastRotateEuler;
        public Vector3 lastScale;
        public bool hasAnyDriven;

        [NonSerialized] public bool initialized;

        public void Initialize(string nodeName, string nodeType)
        {
            mayaNodeName = nodeName ?? "";
            mayaNodeType = nodeType ?? "";
            initialized = true;
        }

        public void SetPlugs(
            string tx, string ty, string tz,
            string rx, string ry, string rz,
            string sx, string sy, string sz)
        {
            txPlug = tx ?? "";
            tyPlug = ty ?? "";
            tzPlug = tz ?? "";
            rxPlug = rx ?? "";
            ryPlug = ry ?? "";
            rzPlug = rz ?? "";
            sxPlug = sx ?? "";
            syPlug = sy ?? "";
            szPlug = sz ?? "";

            hasAnyDriven =
                !string.IsNullOrEmpty(txPlug) || !string.IsNullOrEmpty(tyPlug) || !string.IsNullOrEmpty(tzPlug) ||
                !string.IsNullOrEmpty(rxPlug) || !string.IsNullOrEmpty(ryPlug) || !string.IsNullOrEmpty(rzPlug) ||
                !string.IsNullOrEmpty(sxPlug) || !string.IsNullOrEmpty(syPlug) || !string.IsNullOrEmpty(szPlug);
        }

        public void SetLastValues(Vector3 t, Vector3 rEuler, Vector3 s)
        {
            lastTranslate = t;
            lastRotateEuler = rEuler;
            lastScale = s;
        }
    }
}
