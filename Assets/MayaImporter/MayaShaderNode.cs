// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using System.Collections.Generic;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Shaders
{
    /// <summary>
    /// Base class for all Maya shader-related nodes.
    /// Represents shading, texture and utility nodes.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MayaShaderNode : MayaNodeComponentBase
    {
        // ===== Maya Node Info =====

        [Header("Maya Node Info")]

        [Tooltip("Maya node name")]
        public string mayaNodeName;

        [Tooltip("Maya node type")]
        public string mayaNodeType;

        [Tooltip("Maya node UUID")]
        public string mayaNodeUUID;

        // ===== Attributes =====

        [Header("Attributes")]

        [Tooltip("Float attributes")]
        public Dictionary<string, float> floatAttributes = new Dictionary<string, float>();

        [Tooltip("Int attributes")]
        public Dictionary<string, int> intAttributes = new Dictionary<string, int>();

        [Tooltip("Bool attributes")]
        public Dictionary<string, bool> boolAttributes = new Dictionary<string, bool>();

        [Tooltip("Color attributes")]
        public Dictionary<string, Color> colorAttributes = new Dictionary<string, Color>();

        [Tooltip("Vector attributes")]
        public Dictionary<string, Vector3> vectorAttributes = new Dictionary<string, Vector3>();

        [Tooltip("String attributes")]
        public Dictionary<string, string> stringAttributes = new Dictionary<string, string>();

        // ===== Connections =====

        [Header("Connections")]

        [Tooltip("Input connections (attribute -> source plug)")]
        public Dictionary<string, string> inputConnections = new Dictionary<string, string>();

        [Tooltip("Output connections (attribute -> list of destination plugs)")]
        public Dictionary<string, List<string>> outputConnections = new Dictionary<string, List<string>>();

        /// <summary>
        /// Legacy initializer used by many shader nodes' InitializeXxx() methods.
        /// This keeps Phase-A compatible while your real import path uses InitializeFromRecord().
        /// </summary>
        protected void InitializeNode(string nodeName, string nodeType, string uuid)
        {
            // Sync to Phase-3 base identity
            NodeName = nodeName;
            NodeType = nodeType;
            Uuid = uuid;

            // Keep legacy fields too
            mayaNodeName = nodeName;
            mayaNodeType = nodeType;
            mayaNodeUUID = uuid;
        }

        // ===== Methods =====

        public void SetFloat(string attributeName, float value) => floatAttributes[attributeName] = value;
        public void SetInt(string attributeName, int value) => intAttributes[attributeName] = value;
        public void SetBool(string attributeName, bool value) => boolAttributes[attributeName] = value;
        public void SetColor(string attributeName, Color value) => colorAttributes[attributeName] = value;
        public void SetVector(string attributeName, Vector3 value) => vectorAttributes[attributeName] = value;
        public void SetString(string attributeName, string value) => stringAttributes[attributeName] = value;

        public bool TryGetFloat(string attributeName, out float value) => floatAttributes.TryGetValue(attributeName, out value);
        public bool TryGetInt(string attributeName, out int value) => intAttributes.TryGetValue(attributeName, out value);
        public bool TryGetBool(string attributeName, out bool value) => boolAttributes.TryGetValue(attributeName, out value);
        public bool TryGetColor(string attributeName, out Color value) => colorAttributes.TryGetValue(attributeName, out value);
        public bool TryGetVector(string attributeName, out Vector3 value) => vectorAttributes.TryGetValue(attributeName, out value);
        public bool TryGetString(string attributeName, out string value) => stringAttributes.TryGetValue(attributeName, out value);

        // ===== Connections =====

        public void AddInputConnection(string targetNodeAttribute, string sourceNodeAttribute)
        {
            inputConnections[targetNodeAttribute] = sourceNodeAttribute;
        }

        public void AddOutputConnection(string sourceNodeAttribute, string targetNodeAttribute)
        {
            if (!outputConnections.TryGetValue(sourceNodeAttribute, out var list))
            {
                list = new List<string>();
                outputConnections[sourceNodeAttribute] = list;
            }
            list.Add(targetNodeAttribute);
        }
    }
}
