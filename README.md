# MAYAtoUnity

MAYAtoUnity is a Unity-side importer and reconstruction tool for Maya scene files.  
It parses Maya `.ma` / `.mb` data without relying on Autodesk Maya or the Maya API, stores the recovered scene as structured data, and rebuilds it as a Unity GameObject hierarchy.

This project focuses on **DCC-to-Unity pipeline tooling**: preserving Maya node identity, attributes, connections, and reconstruction evidence so that imported data can be inspected, debugged, and extended inside Unity.

---

## Goals

- Import Maya scene data into Unity without requiring Maya to be installed.
- Preserve Maya node structure, raw attributes, and plug connections as inspectable Unity components.
- Reconstruct a deterministic Unity hierarchy from parsed Maya data.
- Support both text-based `.ma` files and best-effort recovery from binary `.mb` files.
- Provide a portfolio-quality example of technical art / tools engineering for game development pipelines.

---

## What this tool does

MAYAtoUnity converts Maya scene information into Unity through the following pipeline:

```text
.ma / .mb
  ↓
MayaAsciiParser / MayaBinaryParser
  ↓
MayaSceneData
  ↓
NodeRecord / ConnectionRecord / RawAttributeValue
  ↓
UnitySceneBuilder
  ↓
GameObject hierarchy + MayaNodeComponentBase components
  ↓
RuntimeAutoFix / RuntimeAutoBind
```

Each Maya node is represented by a Unity component derived from `MayaNodeComponentBase`.  
The component stores Maya-side identity and source data such as:

- Node name
- Node type
- Parent name
- UUID
- Raw attributes
- Related source / destination connections
- Recovery provenance for `.mb` imports

This makes the import result inspectable even when a Maya node cannot yet be fully simulated in Unity.

---

## Key features

### `.ma` ASCII parsing

The ASCII parser reads Maya command-style scene data and extracts structured records from commands such as:

- `createNode`
- `setAttr`
- `connectAttr`
- `parent`
- `currentUnit`
- `rename`
- `fileInfo`
- `requires`
- `addAttr`
- `deleteAttr`
- animation-related commands such as `setKeyframe`, `setDrivenKeyframe`, and `animLayer`

Parsed attributes are stored as raw tokens and, when possible, typed values such as bool, int, float, vectors, matrices, and string arrays.

### `.mb` binary best-effort recovery

`.mb` files are handled through a preservation-first pipeline.  
Because Maya binary files are not a public, simple interchange format, this importer does not claim perfect `.mb` compatibility. Instead, it keeps the original binary bytes and attempts multiple Unity-only recovery passes:

- Raw binary preservation
- SHA-256 source hash generation
- IFF-like chunk indexing
- Embedded ASCII command extraction
- Null-terminated ASCII reconstruction
- Deterministic node enumeration from string data
- Shading / texture hint tagging
- Mesh topology hint extraction
- Heuristic graph reconstruction
- Placeholder node generation when full recovery is not possible

Recovered nodes are marked with provenance information such as:

- ASCII commands
- Embedded `.mb` ASCII
- Null-terminated `.mb` ASCII
- Deterministic string-table enumeration
- Chunk placeholder fallback
- Heuristic recovery

This makes the result auditable instead of silently pretending that every node was fully decoded.

### Unity hierarchy reconstruction

`UnitySceneBuilder` creates a deterministic Unity hierarchy from `MayaSceneData`.

The build process:

1. Sorts Maya nodes deterministically.
2. Creates GameObjects for each node.
3. Adds node-specific Unity components through a factory.
4. Restores parent-child relationships.
5. Applies Unity reconstruction in staged order.
6. Runs material and runtime post-processors.

The staged rebuild order prioritizes core scene structure first, then cameras, lights, meshes, deformers, constraints, shaders, and animation-related nodes.

### Runtime repair and auto-binding

The runtime helpers are designed to make imported prefabs more robust after loading:

- Automatically adds missing helper components where possible.
- Rebinds constraint-like relationships when source connections can be inferred.
- Performs best-effort fixes for blend shape / skinned mesh related nodes.
- Keeps imported nodes inspectable at runtime.

---

## Supported / current status

| Area | Status | Notes |
|---|---:|---|
| `.ma` parsing | Supported | Command-based parsing for common Maya scene statements. |
| `.mb` parsing | Best-effort | Uses multiple recovery passes and preserves raw binary as source of truth. |
| Node identity | Supported | Node name, type, parent, UUID, attributes, and connections are stored. |
| Transform hierarchy | Supported / partial | Reconstructed from parent information when available. |
| Attributes | Supported | Raw tokens are preserved; common value types are parsed when possible. |
| Connections | Supported | Source / destination plug relationships are stored per node. |
| Materials / shading | Partial | Hints and post-processing are implemented; exact Maya shader parity is not guaranteed. |
| Meshes | Partial | Mesh-related hints and node reconstruction exist; exact geometry support depends on source data. |
| Constraints | Partial | Runtime auto-bind supports simple cases. |
| Blend shapes / skinning | Partial | Runtime repair and node handling exist, but full Maya deformer parity is not guaranteed. |
| Animation | Experimental | Animation command records and optional clip-related paths are present. |

---

## Installation

1. Open a Unity project.
2. Copy or clone this repository into the project so that `Assets/MayaImporter` exists under the Unity `Assets` folder.
3. Let Unity compile the scripts.
4. Place Maya `.ma` or `.mb` files inside the project, or pass an absolute file path to the importer entry point.

---

## Basic usage

The main entry point is `MayaImporter`.

```csharp
using MayaImporter.Core;
using UnityEngine;

public class ImportExample : MonoBehaviour
{
    public string mayaFilePath;

    private void Start()
    {
        var options = new MayaImportOptions();

        MayaSceneData scene;
        MayaImportLog log;

        GameObject root = MayaImporter.ImportIntoScene(
            mayaFilePath,
            options,
            out scene,
            out log
        );

        Debug.Log($"Imported root: {root.name}");
        Debug.Log($"Recovered nodes: {scene.Nodes.Count}");
    }
}
```

For Unity project asset paths, `TryGetAbsolutePathFromAssetPath` can be used to convert `Assets/...` paths into absolute file paths.

---

## Import options

`MayaImportOptions` controls import behavior. Important options include:

| Option | Purpose |
|---|---|
| `CreateRootGameObject` | Creates a single root GameObject for the imported scene. |
| `Conversion` | Controls Maya-to-Unity coordinate conversion. |
| `CreateUnityComponents` | Enables Unity component creation for supported Maya node types. |
| `SaveAssets` | Enables editor-side saving of generated assets. |
| `OutputFolder` | Destination folder for generated Unity assets. |
| `SaveMeshes` | Saves generated mesh assets. |
| `SaveMaterials` | Saves generated material assets. |
| `SaveTextures` | Saves generated texture assets. |
| `SavePrefab` | Saves the imported hierarchy as a prefab. |
| `KeepRawStatements` | Keeps raw command statements for debugging. |
| `MbTryExtractEmbeddedAscii` | Attempts command-like text extraction from `.mb`. |
| `MbTryExtractNullTerminatedAscii` | Attempts command reconstruction from null-terminated strings. |
| `MbDeterministicEnumerateNodes` | Enables deterministic node enumeration from `.mb` string data. |
| `MbCreateChunkPlaceholderNodes` | Creates fallback placeholder nodes if no nodes can be recovered. |

---

## Technical highlights

- Unity-only Maya scene parser without Autodesk / Maya API dependency.
- Preservation-first data model that keeps raw source evidence alongside reconstructed data.
- Deterministic build order for reproducible import results.
- Node-level provenance tracking for `.mb` recovery.
- Inspector-friendly raw attribute and connection previews.
- Generic fallback components for unsupported Maya node types.
- Runtime repair layer for imported prefabs.

---

## Repository structure

```text
Assets/
  MayaImporter/
    Core and parser scripts
    Maya scene data models
    Unity scene builders
    Runtime repair / auto-bind helpers
    Node component implementations
```

---

## Limitations

This project is a custom importer and reconstruction system, not a replacement for Autodesk Maya or Unity's official FBX workflow.

Known limitations:

- `.mb` support is best-effort and evidence-based, not full binary format parity.
- Some Maya node types are stored as generic or unknown components.
- Exact rendering parity with Maya materials is not guaranteed.
- Full deformer, rig, animation, and constraint simulation is still a work in progress.
- Production use requires validation against real project assets.

---

## Roadmap

- Add sample `.ma` / `.mb` files for reproducible demonstrations.
- Add before / after screenshots comparing Maya hierarchy and Unity hierarchy.
- Add import reports showing node counts, connection counts, and recovery provenance breakdown.
- Expand mesh, material, rig, and animation reconstruction coverage.
- Add automated parser tests for representative Maya command files.
- Add a dedicated Unity Editor import window for easier portfolio demonstration.

---

## Portfolio focus

This project demonstrates:

- Unity Editor / runtime tooling
- DCC pipeline engineering
- Parser and data-model design
- Scene graph reconstruction
- Robust fallback design
- Debuggable import pipelines
- Technical artist / tools programmer problem solving
