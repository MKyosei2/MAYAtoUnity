using System;

namespace MayaImporter
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class MayaNodeTypeAttribute : Attribute
    {
        public string NodeType { get; }

        public MayaNodeTypeAttribute(string nodeType)
        {
            NodeType = nodeType;
        }
    }
}
