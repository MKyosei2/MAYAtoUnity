using System;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Helper for legacy shader-network tools.
    /// It tries to attach a ShaderNodeComponentBase-derived component by Maya nodeType.
    /// If the resolved type is not ShaderNodeComponentBase, falls back to UnknownShaderNodeComponent.
    /// </summary>
    public static class ShaderNodeMapper
    {
        public static ShaderNodeComponentBase AttachComponent(GameObject go, string mayaNodeType)
        {
            if (go == null) return null;

            var t = NodeFactory.ResolveType(mayaNodeType);

            ShaderNodeComponentBase comp = null;

            if (t != null && typeof(ShaderNodeComponentBase).IsAssignableFrom(t))
            {
                comp = go.AddComponent(t) as ShaderNodeComponentBase;
            }

            if (comp == null)
            {
                comp = go.AddComponent<UnknownShaderNodeComponent>();
            }

            comp.MayaNodeType = mayaNodeType;
            return comp;
        }
    }
}
