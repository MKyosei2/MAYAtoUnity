using System;

namespace MayaImporter.Core
{
    /// <summary>
    /// Import options for Maya .ma/.mb.
    /// Absolute condition:
    /// - No Autodesk/Maya API. Unity-only.
    /// </summary>
    [Serializable]
    public sealed class MayaImportOptions
    {
        /// <summary>
        /// Store all original statements in SceneData (higher memory, better debugging).
        /// </summary>
        public bool KeepRawStatements = true;

        /// <summary>
        /// Create a single root GameObject named after the file.
        /// </summary>
        public bool CreateRootGameObject = true;

        /// <summary>
        /// Attempt a simple right-handed (Maya) to left-handed (Unity) conversion.
        /// This is heuristic; you can disable it to get 1:1 numbers.
        /// </summary>
        public CoordinateConversion Conversion = CoordinateConversion.MayaToUnity_MirrorZ;

        /// <summary>
        /// If true, creates Unity Camera/Light components for matching Maya nodes.
        /// </summary>
        public bool CreateUnityComponents = true;

        // =========================================================
        // Editor-side asset saving
        // =========================================================

        /// <summary>
        /// If true, after ImportIntoScene, Editor will save Mesh/Material/Texture/Anim/Prefab into Assets.
        /// </summary>
        public bool SaveAssets = true;

        /// <summary>
        /// Must be under "Assets/..." to be saved into the Unity project.
        /// Example: "Assets/MayaImported"
        /// </summary>
        public string OutputFolder = "Assets/MayaImported";

        public bool SaveMeshes = true;
        public bool SaveMaterials = true;
        public bool SaveTextures = true;

        /// <summary>
        /// If true, build legacy AnimationClip from animCurveTL/TA/TU nodes and save it.
        /// </summary>
        public bool SaveAnimationClip = false;

        public string AnimationClipName = "MayaClip";
        public float AnimationTimeScale = 1.0f;

        /// <summary>
        /// If true, save the imported root as a Prefab asset.
        /// </summary>
        public bool SavePrefab = true;

        /// <summary>
        /// If false, importer will delete the scene instance after saving Prefab/Assets.
        /// </summary>
        public bool KeepImportedRootInScene = true;

        // =========================================================
        // Portfolio / Proof components (Unity-only verification)
        // =========================================================

        public bool AttachOpaqueRuntimeMarker = true;
        public bool AttachOpaqueAttributePreview = true;
        public bool AttachOpaqueConnectionPreview = true;
        public bool AttachDecodedAttributeSummary = true;

        /// <summary>
        /// Inspector previews are bounded to keep editor responsive.
        /// </summary>
        public int OpaquePreviewMaxEntries = 128;

        public float OpaqueRuntimeGizmoSize = 0.05f;

        // =========================================================
        // .mb (Binary) specific
        // =========================================================

        /// <summary>
        /// Try extracting embedded command-like ASCII stream from .mb.
        /// If extracted, it is parsed by MayaAsciiParser and merged into the scene (best-effort).
        /// </summary>
        public bool MbTryExtractEmbeddedAscii = true;

        /// <summary>
        /// If true, even low-confidence extracted ASCII will be parsed.
        /// This can improve coverage, but may introduce noisy/incorrect nodes on rare files.
        /// (RawBinaryBytes are always preserved, so you can audit.)
        /// </summary>
        public bool MbAllowLowConfidenceEmbeddedAscii = true;

        /// <summary>
        /// Hard accept if statements >= this.
        /// </summary>
        public int MbEmbeddedAsciiHardMinStatements = 30;

        /// <summary>
        /// Soft accept if statements >= this AND score >= MbEmbeddedAsciiMinScore.
        /// </summary>
        public int MbEmbeddedAsciiMinStatements = 12;

        /// <summary>
        /// Score threshold for soft accept.
        /// </summary>
        public int MbEmbeddedAsciiMinScore = 24;

        /// <summary>
        /// Hard accept if score >= this.
        /// </summary>
        public int MbEmbeddedAsciiHardMinScore = 60;

        /// <summary>
        /// Max extracted chars (safety).
        /// </summary>
        public int MbEmbeddedAsciiMaxChars = 4 * 1024 * 1024;
    }

    public enum CoordinateConversion
    {
        None = 0,

        /// <summary>
        /// Mirror Z axis (x,y,-z) and adjust Euler (x,-y,-z).
        /// Common quick conversion between Maya RH and Unity LH.
        /// </summary>
        MayaToUnity_MirrorZ = 1,
    }
}
