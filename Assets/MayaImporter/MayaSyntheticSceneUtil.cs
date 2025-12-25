using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MayaImporter.Core
{
    /// <summary>
    /// Unity-only smoke scene builder utilities.
    /// Creates MayaSceneData/NodeRecord that mimic .ma/.mb parse results.
    /// </summary>
    public static class MayaSyntheticSceneUtil
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static NodeRecord AddNode(MayaSceneData scene, string nodeName, string nodeType, string parentName = null, string uuid = null)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (string.IsNullOrEmpty(nodeName)) throw new ArgumentException("nodeName is null/empty");
            if (string.IsNullOrEmpty(nodeType)) nodeType = "unknown";

            var rec = scene.GetOrCreateNode(nodeName, nodeType);
            rec.NodeType = nodeType;
            rec.ParentName = parentName;
            rec.Uuid = uuid;
            return rec;
        }

        public static void SetAttrTokens(NodeRecord rec, string key, params string[] tokens)
            => SetAttrTokens(rec, key, typeName: null, tokens: tokens);

        public static void SetAttrTokens(NodeRecord rec, string key, string typeName, IEnumerable<string> tokens)
        {
            if (rec == null) throw new ArgumentNullException(nameof(rec));
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key is null/empty");

            var list = new List<string>();
            if (tokens != null)
            {
                foreach (var t in tokens)
                {
                    if (t == null) continue;
                    list.Add(t);
                }
            }

            var raw = new RawAttributeValue(typeName, list);
            rec.Attributes[key] = raw;
        }

        public static void SetFloat1(NodeRecord rec, string key, float v)
            => SetAttrTokens(rec, key, v.ToString("R", Inv));

        public static void SetFloat2(NodeRecord rec, string key, Vector2 v)
            => SetAttrTokens(rec, key,
                v.x.ToString("R", Inv),
                v.y.ToString("R", Inv));

        public static void SetFloat3(NodeRecord rec, string key, Vector3 v)
            => SetAttrTokens(rec, key,
                v.x.ToString("R", Inv),
                v.y.ToString("R", Inv),
                v.z.ToString("R", Inv));

        public static void SetColor3(NodeRecord rec, string key, Color c)
            => SetAttrTokens(rec, key,
                c.r.ToString("R", Inv),
                c.g.ToString("R", Inv),
                c.b.ToString("R", Inv));

        public static void AddConnection(MayaSceneData scene, string srcPlug, string dstPlug, bool force = false)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (string.IsNullOrEmpty(srcPlug)) throw new ArgumentException("srcPlug is null/empty");
            if (string.IsNullOrEmpty(dstPlug)) throw new ArgumentException("dstPlug is null/empty");

            scene.Connections.Add(new ConnectionRecord(srcPlug, dstPlug, force));
        }

        /// <summary>
        /// Adds a raw "sets -e -forceElement SG member..." statement.
        /// ShadingEngineNode prefers this to determine members.
        /// </summary>
        public static void AddRawSetsForceElement(MayaSceneData scene, string shadingEngineName, params string[] members)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (string.IsNullOrEmpty(shadingEngineName)) throw new ArgumentException("shadingEngineName is null/empty");

            var toks = new List<string>
            {
                "sets",
                "-e",
                "-forceElement",
                shadingEngineName
            };

            if (members != null)
            {
                for (int i = 0; i < members.Length; i++)
                {
                    var m = members[i];
                    if (string.IsNullOrEmpty(m)) continue;
                    toks.Add(m);
                }
            }

            scene.RawStatements.Add(new RawStatement
            {
                Command = "sets",
                Text = string.Join(" ", toks),
                Tokens = toks
            });
        }
    }
}
