// MAYAIMPORTER_PATCH_V4: mb provenance/evidence + audit determinism (generated 2026-01-05)
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
        /// Additional .mb recovery path (Unity-only):
        /// Extracts null-terminated ASCII strings from the binary and tries to reconstruct
        /// MEL-like statements (createNode/setAttr/connectAttr...).
        /// This is best-effort and is only used to increase coverage when embedded-ascii scan
        /// does not yield enough statements.
        /// </summary>
        public bool MbTryExtractNullTerminatedAscii = true;

        /// <summary>
        /// If true, the null-terminated extractor will be allowed even on low confidence.
        /// </summary>
        public bool MbAllowLowConfidenceNullTerminatedAscii = true;

        /// <summary>
        /// If reconstructed statements >= this, we always parse.
        /// </summary>
        public int MbNullTerminatedHardMinStatements = 10;

        /// <summary>
        /// Max reconstructed statements (safety).
        /// </summary>
        public int MbNullTerminatedMaxStatements = 200000;

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

        
        // =========================================================
        // .mb Phase0.5: Deterministic node enumeration (Unity-only, additive)
        // =========================================================

        /// <summary>
        /// If true, enumerate nodes deterministically from .mb extracted strings (string table, DAG-like paths),
        /// and (optionally) command-like text recovered from the binary.
        /// This improves inspection/audit even when embedded ASCII is unavailable.
        /// </summary>
        public bool MbDeterministicEnumerateNodes = true;

        /// <summary>
        /// Safety: maximum number of nodes created by deterministic enumeration.
        /// </summary>
        public int MbDeterministicMaxNodes = 50000;

        /// <summary>
        /// Safety: maximum number of DAG paths considered.
        /// </summary>
        public int MbDeterministicMaxDagPaths = 250000;

        /// <summary>
        /// When associating name/type hints, search in a small window around the string index.
        /// </summary>
        public int MbDeterministicAssociationWindow = 64;

        /// <summary>
        /// If true, allow single-token names (non-DAG) as deterministic node candidates.
        /// Kept OFF by default to avoid noise.
        /// </summary>
        public bool MbDeterministicAllowSingleNames = false;

        /// <summary>
        /// Best-effort: tag shading/texture hints from .mb string table for materials stage.
        /// </summary>
        public bool MbTryTagShadingFromStrings = true;

        public bool MbTryTagTexturesFromStrings = true;

// =========================================================
        // .mb Phase1: preservation-first fallback
        // =========================================================

        /// <summary>
        /// If .mb parsing cannot enumerate nodes, create placeholder NodeRecords from
        /// the binary chunk index so the Unity reconstruction has a stable, inspectable
        /// structure while keeping RawBinaryBytes as the source of truth.
        /// </summary>
        public bool MbCreateChunkPlaceholderNodes = true;

        /// <summary>
        /// Max placeholder nodes generated from chunk index (safety).
        /// </summary>
        public int MbChunkPlaceholderMaxNodes = 20000;
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
