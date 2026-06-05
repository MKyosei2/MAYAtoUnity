// MAYAIMPORTER_PATCH_V6: JSON bridge data models for geometry/material/camera/light/animation
using System;

namespace MayaImporter.Core
{
    [Serializable]
    public sealed class MayaUnityExport
    {
        public int schemaVersion;
        public string sourceMayaFile;
        public string sourceHash;
        public MayaUnityExportUnits units;
        public MayaUnityExportNode[] nodes;
        public MayaUnityExportTransform[] transforms;
        public MayaUnityExportMesh[] meshes;
        public MayaUnityExportMaterial[] materials;
        public MayaUnityExportTexture[] textures;
        public MayaUnityExportCamera[] cameras;
        public MayaUnityExportLight[] lights;
        public MayaUnityExportJoint[] joints;
        public MayaUnityExportAnimationCurve[] animations;
        public MayaUnityExportUnsupported[] unsupported;
    }

    [Serializable]
    public sealed class MayaUnityExportUnits
    {
        public string linear;
        public string angle;
        public string time;
    }

    [Serializable]
    public class MayaUnityExportNode
    {
        public string name;
        public string path;
        public string type;
        public string uuid;
        public string parentPath;
    }

    [Serializable]
    public class MayaUnityExportTransform : MayaUnityExportNode
    {
        public float[] localPosition;
        public float[] localRotation;
        public float[] localScale;
        public float[] localMatrix;
        public float[] worldMatrix;
    }

    [Serializable]
    public sealed class MayaUnityExportMesh : MayaUnityExportNode
    {
        public int vertexCount;
        public int triangleCount;
        public int sourceVertexCount;
        public int sourceTriangleCount;
        public float[] vertices;
        public float[] normals;
        public float[] uvs;
        public int[] indices;
        public string[] materials;
    }

    [Serializable]
    public sealed class MayaUnityExportMaterial
    {
        public string name;
        public string type;
        public string uuid;
        public float[] color;
        public float[] transparency;
        public string diffuseTexture;
    }

    [Serializable]
    public sealed class MayaUnityExportTexture
    {
        public string name;
        public string type;
        public string uuid;
        public string fileTextureName;
        public string colorSpace;
    }

    [Serializable]
    public sealed class MayaUnityExportCamera : MayaUnityExportNode
    {
        public float focalLength;
        public float horizontalFilmAperture;
        public float verticalFilmAperture;
        public float nearClipPlane;
        public float farClipPlane;
    }

    [Serializable]
    public sealed class MayaUnityExportLight : MayaUnityExportNode
    {
        public float[] color;
        public float intensity;
        public float coneAngle;
        public float penumbraAngle;
        public float dropoff;
    }

    [Serializable]
    public sealed class MayaUnityExportJoint : MayaUnityExportTransform
    {
        public float[] jointOrient;
    }

    [Serializable]
    public sealed class MayaUnityExportAnimationCurve
    {
        public string targetPath;
        public string attribute;
        public string unityProperty;
        public float[] times;
        public float[] values;
    }

    [Serializable]
    public sealed class MayaUnityExportUnsupported
    {
        public string name;
        public string type;
        public string reason;
    }
}
