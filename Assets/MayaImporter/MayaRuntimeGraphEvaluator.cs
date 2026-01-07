using System;
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Runtime evaluator that can drive Unity transforms from Maya DG connections.
    /// - Works without Autodesk/Maya API (Unity-only)
    /// - Uses MayaAnimValueGraph (animCurve + common compute nodes) to evaluate plugs
    /// - Updates TRS for nodes of type transform/joint when those channels are connection-driven
    ///
    /// Notes:
    /// - Best-effort: aims for "Unity上で再構築できる" rather than perfect Maya DG parity.
    /// - Safe even when nothing is driven (stays idle).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MayaRuntimeGraphEvaluator : MonoBehaviour
    {
        [Header("Enable")]
        public bool enableRuntimeEvaluation = true;

        [Tooltip("When false, evaluator only builds inspector bindings but does not update transforms.")]
        public bool runInEditMode = false;

        [Header("Debug")]
        public int totalNodeComponents;
        public int totalConnections;
        public int drivenTransformCount;

        [Serializable]
        public sealed class DrivenTransformBinding
        {
            public Transform unityTransform;
            public MayaNodeComponentBase mayaNode;
            public MayaEulerRotationApplier eulerApplier;
            public MayaDrivenTransformRuntime marker;

            public string tx;
            public string ty;
            public string tz;
            public string rx;
            public string ry;
            public string rz;
            public string sx;
            public string sy;
            public string sz;

            public Vector3 baseT;
            public Vector3 baseR;
            public Vector3 baseS;

            public bool HasAnyDriven =>
                !string.IsNullOrEmpty(tx) || !string.IsNullOrEmpty(ty) || !string.IsNullOrEmpty(tz) ||
                !string.IsNullOrEmpty(rx) || !string.IsNullOrEmpty(ry) || !string.IsNullOrEmpty(rz) ||
                !string.IsNullOrEmpty(sx) || !string.IsNullOrEmpty(sy) || !string.IsNullOrEmpty(sz);
        }

        [SerializeField] private List<DrivenTransformBinding> _driven = new List<DrivenTransformBinding>(256);

        private MayaAnimValueGraph _graph;
        private MayaTimeEvaluationPlayer _player;
        private MayaSceneSettings _settings;
        private MayaImportOptions _buildOptionsSnapshot;

        private void OnEnable()
        {
            _player = GetComponent<MayaTimeEvaluationPlayer>();
            if (_player != null)
            {
                _player.AfterSample -= OnAfterSample;
                _player.AfterSample += OnAfterSample;
            }
        }

        private void OnDisable()
        {
            if (_player != null)
                _player.AfterSample -= OnAfterSample;
        }

        private void Start()
        {
            // In case phase3 setup wasn't called (manual scene usage), build here.
            Build_BestEffort(MayaBuildContext.CurrentOptions ?? new MayaImportOptions(),
                             MayaBuildContext.CurrentLog ?? new MayaImportLog());
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Editor safety: do not animate scene unless explicitly requested
            if (!Application.isPlaying)
                return;

            // If player exists, we evaluate via AfterSample callback.
            if (_player != null)
                return;

            if (enableRuntimeEvaluation)
            {
                float fps = (float)(_settings != null ? _settings.framesPerSecond : 24.0);
                float frame = Time.time * Mathf.Max(1f, fps);
                EvaluateAtFrame(frame);
            }
        }
#else
        private void Update()
        {
            if (_player != null)
                return;

            if (enableRuntimeEvaluation)
            {
                float fps = (float)(_settings != null ? _settings.framesPerSecond : 24.0);
                float frame = Time.time * Mathf.Max(1f, fps);
                EvaluateAtFrame(frame);
            }
        }
#endif

        /// <summary>
        /// Called from Phase3 setup at import-time.
        /// Can also be called at runtime to rebuild (e.g., after manual edits).
        /// </summary>
        public void Build_BestEffort(MayaImportOptions options, MayaImportLog log)
        {
            options ??= new MayaImportOptions();
            log ??= new MayaImportLog();

            _buildOptionsSnapshot = options;
            _settings = GetComponent<MayaSceneSettings>();

            try
            {
                var nodes = GetComponentsInChildren<MayaNodeComponentBase>(true);
                totalNodeComponents = nodes != null ? nodes.Length : 0;

                var curves = new List<MayaAnimCurveNodeComponent>(256);
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                        if (nodes[i] is MayaAnimCurveNodeComponent ac)
                            curves.Add(ac);
                }

                // Build a de-duplicated global connections list from per-node Connections.
                var scene = new MayaSceneData();
                var dedup = new HashSet<string>(StringComparer.Ordinal);

                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var n = nodes[i];
                        if (n == null || n.Connections == null) continue;

                        for (int c = 0; c < n.Connections.Count; c++)
                        {
                            var cn = n.Connections[c];
                            if (cn == null) continue;

                            // Only take Destination/Both side to avoid duplicates
                            if (cn.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                                cn.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                                continue;

                            var src = (cn.SrcPlug ?? "").Trim().Trim('"');
                            var dst = (cn.DstPlug ?? "").Trim().Trim('"');
                            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dst)) continue;

                            var k = src + "->" + dst;
                            if (!dedup.Add(k)) continue;

                            scene.Connections.Add(new ConnectionRecord(src, dst, cn.Force));
                        }
                    }
                }

                totalConnections = scene.Connections.Count;

                // Build graph (best-effort compute coverage)
                var nodeList = new List<MayaNodeComponentBase>(nodes ?? Array.Empty<MayaNodeComponentBase>());
                _graph = new MayaAnimValueGraph(scene, nodeList, curves);

                // Build driven TRS bindings
                BuildDrivenTransformBindings(nodes, options, log);

                drivenTransformCount = _driven.Count;

                log.Info($"[Phase3] Graph built. nodes={totalNodeComponents} conns={totalConnections} drivenTransforms={drivenTransformCount}");
            }
            catch (Exception ex)
            {
                log.Warn($"[Phase3] Build graph failed: {ex.GetType().Name}: {ex.Message}");
                _graph = null;
                _driven.Clear();
                drivenTransformCount = 0;
            }
        }

        private void OnAfterSample(float frame, float timeSec)
        {
            if (!enableRuntimeEvaluation) return;
            if (!Application.isPlaying) return;
            EvaluateAtFrame(frame);
        }

        private void EvaluateAtFrame(float frame)
        {
            if (_graph == null || _driven == null || _driven.Count == 0)
                return;

            for (int i = 0; i < _driven.Count; i++)
            {
                var b = _driven[i];
                if (b == null || b.unityTransform == null || b.mayaNode == null)
                    continue;

                // TRS (Maya space)
                Vector3 tMaya = b.baseT;
                Vector3 rMaya = b.baseR;
                Vector3 sMaya = b.baseS;

                if (!string.IsNullOrEmpty(b.tx)) tMaya.x = _graph.EvaluatePlug(b.tx, frame);
                if (!string.IsNullOrEmpty(b.ty)) tMaya.y = _graph.EvaluatePlug(b.ty, frame);
                if (!string.IsNullOrEmpty(b.tz)) tMaya.z = _graph.EvaluatePlug(b.tz, frame);

                if (!string.IsNullOrEmpty(b.rx)) rMaya.x = _graph.EvaluatePlug(b.rx, frame);
                if (!string.IsNullOrEmpty(b.ry)) rMaya.y = _graph.EvaluatePlug(b.ry, frame);
                if (!string.IsNullOrEmpty(b.rz)) rMaya.z = _graph.EvaluatePlug(b.rz, frame);

                if (!string.IsNullOrEmpty(b.sx)) sMaya.x = _graph.EvaluatePlug(b.sx, frame);
                if (!string.IsNullOrEmpty(b.sy)) sMaya.y = _graph.EvaluatePlug(b.sy, frame);
                if (!string.IsNullOrEmpty(b.sz)) sMaya.z = _graph.EvaluatePlug(b.sz, frame);

                var conv = _buildOptionsSnapshot != null
                    ? _buildOptionsSnapshot.Conversion
                    : CoordinateConversion.MayaToUnity_MirrorZ;

                var tUnity = MayaToUnityConversion.ConvertPosition(tMaya, conv);
                b.unityTransform.localPosition = tUnity;
                b.unityTransform.localScale = sMaya;

                if (b.eulerApplier != null)
                {
                    b.eulerApplier.eulerDeg = rMaya;
                }
                else
                {
                    // Fallback: direct quaternion conversion (less accurate for rotateOrder)
                    var q = MayaEulerRotationApplier.ToQuaternion(MayaToUnityConversion.ConvertEulerDegrees(rMaya, conv), 0);
                    b.unityTransform.localRotation = q;
                }

                if (b.marker != null)
                    b.marker.SetLastValues(tMaya, rMaya, sMaya);
            }
        }

        private void BuildDrivenTransformBindings(MayaNodeComponentBase[] nodes, MayaImportOptions options, MayaImportLog log)
        {
            _driven.Clear();

            if (nodes == null || nodes.Length == 0)
                return;

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                var nt = n.NodeType ?? "";
                if (!string.Equals(nt, "transform", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(nt, "joint", StringComparison.OrdinalIgnoreCase))
                    continue;

                var tr = n.transform;

                // Collect dest-plug names for TRS channels.
                string tx = FindIncomingDestPlug(n, "tx", "translateX", ".tx", ".translateX");
                string ty = FindIncomingDestPlug(n, "ty", "translateY", ".ty", ".translateY");
                string tz = FindIncomingDestPlug(n, "tz", "translateZ", ".tz", ".translateZ");

                string rx = FindIncomingDestPlug(n, "rx", "rotateX", ".rx", ".rotateX");
                string ry = FindIncomingDestPlug(n, "ry", "rotateY", ".ry", ".rotateY");
                string rz = FindIncomingDestPlug(n, "rz", "rotateZ", ".rz", ".rotateZ");

                string sx = FindIncomingDestPlug(n, "sx", "scaleX", ".sx", ".scaleX");
                string sy = FindIncomingDestPlug(n, "sy", "scaleY", ".sy", ".scaleY");
                string sz = FindIncomingDestPlug(n, "sz", "scaleZ", ".sz", ".scaleZ");

                // Read base values (Maya space)
                var baseT = ReadVec3(n,
                    packedKeys: new[] { "t", ".t", "translate", ".translate" },
                    xKeys: new[] { "tx", ".tx", "translateX", ".translateX" },
                    yKeys: new[] { "ty", ".ty", "translateY", ".translateY" },
                    zKeys: new[] { "tz", ".tz", "translateZ", ".translateZ" },
                    defaultValue: Vector3.zero);

                var baseR = ReadVec3(n,
                    packedKeys: new[] { "r", ".r", "rotate", ".rotate" },
                    xKeys: new[] { "rx", ".rx", "rotateX", ".rotateX" },
                    yKeys: new[] { "ry", ".ry", "rotateY", ".rotateY" },
                    zKeys: new[] { "rz", ".rz", "rotateZ", ".rotateZ" },
                    defaultValue: Vector3.zero);

                var baseS = ReadVec3(n,
                    packedKeys: new[] { "s", ".s", "scale", ".scale" },
                    xKeys: new[] { "sx", ".sx", "scaleX", ".scaleX" },
                    yKeys: new[] { "sy", ".sy", "scaleY", ".scaleY" },
                    zKeys: new[] { "sz", ".sz", "scaleZ", ".scaleZ" },
                    defaultValue: Vector3.one);

                var binding = new DrivenTransformBinding
                {
                    unityTransform = tr,
                    mayaNode = n,
                    eulerApplier = tr.GetComponent<MayaEulerRotationApplier>(),
                    tx = tx,
                    ty = ty,
                    tz = tz,
                    rx = rx,
                    ry = ry,
                    rz = rz,
                    sx = sx,
                    sy = sy,
                    sz = sz,
                    baseT = baseT,
                    baseR = baseR,
                    baseS = baseS,
                };

                if (!binding.HasAnyDriven)
                    continue;

                // Inspector marker (proof)
                var marker = tr.GetComponent<MayaDrivenTransformRuntime>();
                if (marker == null) marker = tr.gameObject.AddComponent<MayaDrivenTransformRuntime>();
                marker.Initialize(n.NodeName, n.NodeType);
                marker.SetPlugs(tx, ty, tz, rx, ry, rz, sx, sy, sz);
                marker.SetLastValues(baseT, baseR, baseS);
                binding.marker = marker;

                _driven.Add(binding);
            }

            // Deterministic sort
            _driven.Sort((a, b) =>
            {
                string na = a?.mayaNode?.NodeName ?? "";
                string nb = b?.mayaNode?.NodeName ?? "";
                return StringComparer.Ordinal.Compare(na, nb);
            });
        }

        private static string FindIncomingDestPlug(MayaNodeComponentBase node, params string[] attrCandidates)
        {
            if (node == null || node.Connections == null || attrCandidates == null || attrCandidates.Length == 0)
                return null;

            string best = null;

            for (int i = 0; i < node.Connections.Count; i++)
            {
                var c = node.Connections[i];
                if (c == null) continue;

                if (c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Destination &&
                    c.RoleForThisNode != MayaNodeComponentBase.ConnectionRole.Both)
                    continue;

                var dst = (c.DstPlug ?? "").Trim().Trim('"');
                if (string.IsNullOrEmpty(dst)) continue;

                var dstAttr = MayaPlugUtil.ExtractAttrPart(dst);
                if (string.IsNullOrEmpty(dstAttr)) continue;

                for (int k = 0; k < attrCandidates.Length; k++)
                {
                    var cand = attrCandidates[k];
                    if (string.IsNullOrEmpty(cand)) continue;
                    cand = cand.TrimStart('.');

                    if (string.Equals(dstAttr, cand, StringComparison.Ordinal) ||
                        string.Equals(dstAttr, "." + cand, StringComparison.Ordinal))
                    {
                        best = dst;
                        break;
                    }
                }
            }

            return best;
        }

        private static Vector3 ReadVec3(
            MayaNodeComponentBase node,
            string[] packedKeys,
            string[] xKeys,
            string[] yKeys,
            string[] zKeys,
            Vector3 defaultValue)
        {
            if (node == null || node.Attributes == null)
                return defaultValue;

            // 1) Packed
            if (packedKeys != null)
            {
                for (int i = 0; i < packedKeys.Length; i++)
                {
                    if (TryGetTokens(node, packedKeys[i], out var tokens) && tokens != null)
                    {
                        if (TryParseLastNFloats(tokens, 3, out var arr))
                            return new Vector3(arr[0], arr[1], arr[2]);
                    }
                }
            }

            // 2) Axis
            bool hx = TryReadFloat(node, xKeys, out float x);
            bool hy = TryReadFloat(node, yKeys, out float y);
            bool hz = TryReadFloat(node, zKeys, out float z);

            if (!hx && !hy && !hz)
                return defaultValue;

            if (!hx) x = defaultValue.x;
            if (!hy) y = defaultValue.y;
            if (!hz) z = defaultValue.z;

            return new Vector3(x, y, z);
        }

        private static bool TryReadFloat(MayaNodeComponentBase node, string[] keys, out float v)
        {
            v = 0f;
            if (node == null || node.Attributes == null || keys == null || keys.Length == 0)
                return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetTokens(node, keys[i], out var tokens) && tokens != null)
                {
                    for (int t = tokens.Count - 1; t >= 0; t--)
                    {
                        var s = tokens[t];
                        if (string.IsNullOrEmpty(s)) continue;
                        s = s.Trim().Trim('"');
                        if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                        {
                            v = f;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryGetTokens(MayaNodeComponentBase node, string key, out List<string> tokens)
        {
            tokens = null;
            if (node == null || node.Attributes == null || string.IsNullOrEmpty(key))
                return false;

            string k0 = key;
            string k1 = key.StartsWith(".", StringComparison.Ordinal) ? key.Substring(1) : "." + key;

            var attrs = node.Attributes;
            for (int i = 0; i < attrs.Count; i++)
            {
                var a = attrs[i];
                if (a == null || string.IsNullOrEmpty(a.Key)) continue;

                if (string.Equals(a.Key, k0, StringComparison.Ordinal) || string.Equals(a.Key, k1, StringComparison.Ordinal))
                {
                    tokens = a.Tokens;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseLastNFloats(List<string> tokens, int n, out float[] values)
        {
            values = null;
            if (tokens == null || tokens.Count < n) return false;

            var tmp = new List<float>(n);
            for (int i = tokens.Count - 1; i >= 0 && tmp.Count < n; i--)
            {
                var s = tokens[i];
                if (string.IsNullOrEmpty(s)) continue;
                s = s.Trim().Trim('"');
                if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                    tmp.Add(f);
            }

            if (tmp.Count != n) return false;
            tmp.Reverse();
            values = tmp.ToArray();
            return true;
        }
    }
}
