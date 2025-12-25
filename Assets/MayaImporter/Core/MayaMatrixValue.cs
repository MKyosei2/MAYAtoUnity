// Assets/MayaImporter/Core/MayaMatrixValue.cs
// Single authoritative matrix carrier.
//
// Canonical fields are legacy names:
//  - matrixMaya / matrixUnity
//
// Newer property aliases (used by BlendMatrixNode / DecomposeMatrixNode / ComposeMatrixNode):
//  - mayaMatrix / unityMatrix
//
// IMPORTANT:
// Ensure there is NO other class named MayaMatrixValue in other namespaces (e.g. MayaImporter.Nodes).

using UnityEngine;

namespace MayaImporter.Core
{
    [DisallowMultipleComponent]
    public sealed class MayaMatrixValue : MonoBehaviour
    {
        public bool valid;

        // Canonical storage (legacy)
        public Matrix4x4 matrixMaya = Matrix4x4.identity;
        public Matrix4x4 matrixUnity = Matrix4x4.identity;

        // Newer alias names (properties)
        public Matrix4x4 mayaMatrix
        {
            get => matrixMaya;
            set => matrixMaya = value;
        }

        public Matrix4x4 unityMatrix
        {
            get => matrixUnity;
            set => matrixUnity = value;
        }

        public void SetBoth(Matrix4x4 maya, Matrix4x4 unity)
        {
            matrixMaya = maya;
            matrixUnity = unity;
        }
    }
}
