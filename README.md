# MAYAtoUnity

**MAYAtoUnity** is a Unity Editor / DCC pipeline tool for importing, preserving, validating, and reconstructing Maya scene data inside Unity.

This project is designed as a **Technical Artist / Tools Programmer portfolio project**. It focuses on a real production problem: moving DCC scene information into a game engine while keeping enough evidence to debug what was imported, what was approximated, and what is currently unsupported.

> Scope note: this is not a full replacement for Autodesk Maya, FBX, or Unity's official import pipeline. The goal is an inspectable, deterministic, validation-oriented bridge for Maya / DCC data.

---

## 30-second overview

MAYAtoUnity has two import paths:

```text
Path A: Exporter JSON Bridge
Maya scene
  -> Tools/MayaExporter/maya_to_unity_exporter.py
  -> exporter JSON
  -> MayaUnityJsonImporter
  -> Unity hierarchy / mesh / material / animation / skin / blendshape / constraint metadata
  -> Import report + validation log

Path B: Unity-only .ma/.mb preservation
.ma / .mb file
  -> parser / recovery layer
  -> MayaSceneData
  -> UnitySceneBuilder
  -> preserved node records, attributes, connections, provenance, and report
```

The main portfolio value is not only that data can be imported. The important part is that the import process is **traceable**: source nodes, attributes, fallback paths, unsupported information, warnings, and generated Unity objects are recorded for review.

---

## What this demonstrates

- Unity Editor tooling
- DCC pipeline engineering
- Intermediate scene data model design
- Deterministic scene reconstruction
- Mesh topology import
- SubMesh / material assignment
- Texture binding
- Camera / light reconstruction
- AnimationCurve generation
- Constraint baking metadata
- Skin weight / bindpose import
- BlendShape delta import
- Import report and validation workflow
- Best-effort `.mb` preservation / recovery
- Graceful degradation for unsupported features

---

## Reviewer path

For a quick review, start here:

```text
1. Read this README
2. Open the Unity project
3. Run: Tools/MAYAtoUnity/Validate All Samples
4. Inspect generated import reports
5. Open Samples/ExporterJson/SimpleMeshMaterialAnimation.json
6. Review Assets/MayaImporter/MayaUnityJsonImporter.cs
7. Review Assets/MayaImporter/MayaUnityJsonRuntimeBuilder.cs
8. Review Docs/ValidationWorkflow.md
```

Expected validation entry points:

```text
Tools/MAYAtoUnity/Validate All Samples
Tools/MAYAtoUnity/Validate Selected Exporter JSON
```

---

## Current JSON Bridge support

| Area | Status | Notes |
|---|---:|---|
| Transform hierarchy | Implemented | Builds Unity hierarchy from Maya path / parent path |
| Mesh topology | Implemented | Reconstructs vertices, normals, UVs, triangle indices |
| SubMesh | Implemented | Converts face material assignment to Unity submeshes |
| Material color | Implemented | Supports lambert / phong / blinn / standardSurface-style base color mapping |
| Texture binding | Implemented | Maps file texture path to `_BaseMap` / `_MainTex` where possible |
| Camera | Implemented | Preserves focal length and clipping plane metadata |
| Light | Implemented | Directional / point / spot / area fallback support |
| Animation curves | Implemented | Converts translate / rotate / scale animCurve data into Unity AnimationClip curves |
| Constraint metadata | Implemented | Stores constraint nodes and bake status in scene data |
| Constraint baking | Implemented | Uses baked transform animation rather than reimplementing Maya solvers |
| Joints | Implemented | Preserves joint hierarchy / jointOrient / matrix metadata |
| Skin weights | Implemented | Converts top 4 weights per exported vertex into Unity BoneWeight |
| Bindposes | Implemented | Uses skinCluster bindPreMatrix when available, fallback to joint inverse matrix |
| SkinnedMeshRenderer | Implemented | Builds skinned renderer when skin data exists |
| BlendShape deltas | Implemented | Imports sampled delta vertices into Unity blendshape frames |
| Current BlendShape weight | Implemented | Applies current weight to SkinnedMeshRenderer |
| Import report | Implemented | Writes source evidence, coverage, unsupported entries, and log output |
| Validation menu | Implemented | Editor menu for sample import / report generation |

---

## Unity-only `.ma/.mb` preservation path

MAYAtoUnity also preserves Maya ASCII / Maya Binary information without calling Maya.

### `.ma` path

```text
.ma file
  -> MayaAsciiParser
  -> createNode / setAttr / connectAttr / parent / currentUnit records
  -> MayaSceneData
  -> UnitySceneBuilder
```

Preserved data includes:

- node name / node type
- parent hierarchy
- UUID
- raw attributes
- typed parsed attributes
- plug connections
- raw command statements
- unit information

### `.mb` path

`.mb` is handled as best-effort preservation / recovery rather than a full binary Maya parser.

```text
.mb bytes
  -> raw binary preservation + SHA-256
  -> IFF-like chunk index
  -> embedded ASCII extraction
  -> null-terminated ASCII reconstruction
  -> string table / DAG-like path hints
  -> deterministic node enumeration
  -> chunk placeholder fallback
```

Provenance categories are recorded so reviewers can see where each reconstructed record came from.

| Provenance | Meaning |
|---|---|
| `AsciiCommands` | `.ma` command or exporter JSON source |
| `MbEmbeddedAscii` | command-like text found inside `.mb` |
| `MbNullTerminatedAscii` | recovered from null-terminated strings |
| `MbDeterministicStringTable` | deterministic enumeration from string table |
| `MbChunkPlaceholder` | placeholder from chunk index |
| `MbHeuristic` | heuristic reconstruction |

---

## Architecture

```text
Assets/MayaImporter/
  MayaImporter.cs                    # public import entry point
  MayaImportOptions.cs               # import configuration
  MayaImportReport.cs                # Markdown report writer
  MayaUnityJsonImporter.cs           # JSON bridge entry point
  MayaUnityJsonModels.cs             # exporter JSON model
  MayaUnityJsonMeshBuilder.cs        # Unity Mesh construction
  MayaUnityJsonRuntimeBuilder.cs     # renderer/material/camera/light/skin/blendshape attachment
  MayaUnityJsonSceneConverter.cs     # exporter JSON -> MayaSceneData

Assets/MayaImporter/Editor/
  MayaImportValidationMenu.cs        # sample validation menu

Tools/MayaExporter/
  maya_to_unity_exporter.py          # Maya-side JSON exporter

Samples/
  SimpleHierarchy.ma
  MaterialTexture.ma
  CameraLight.ma
  TransformAnimation.ma
  ConstraintSample.ma
  ExporterJson/SimpleMeshMaterialAnimation.json

Docs/
  SupportedNodes.md
  MayaExporterPlan.md
  ValidationWorkflow.md
```

---

## Active performance work

A performance branch / PR is being prepared to reduce repeated hierarchy scans during JSON runtime attachment.

Planned / active changes:

- `MayaImportContext` for one import-wide transform/component cache
- one hierarchy index reused by mesh, skin, blendshape, material, camera, and light stages
- runtime/editor `.asmdef` split to reduce Assembly-CSharp coupling
- future import stage timing report

Target evidence to add after Unity validation:

```text
JSON parse:        xx ms
Scene conversion:  xx ms
Hierarchy build:   xx ms
Mesh build:        xx ms
Skin bind:         xx ms
BlendShape bind:   xx ms
Material assign:   xx ms
Camera/Light:      xx ms
Report write:      xx ms
Total:             xx ms
```

---

## How to validate

Open the Unity project and run:

```text
Tools/MAYAtoUnity/Validate All Samples
```

Or select an exporter JSON file and run:

```text
Tools/MAYAtoUnity/Validate Selected Exporter JSON
```

Recommended sample:

```text
Samples/ExporterJson/SimpleMeshMaterialAnimation.json
```

The validation path imports the file, builds Unity objects, writes an import report, and logs node / connection counts.

---

## Programmatic import example

```csharp
using MayaImporter.Core;
using UnityEngine;

public class MayaJsonImportExample : MonoBehaviour
{
    public string jsonPath;

    private void Start()
    {
        var options = new MayaImportOptions
        {
            GenerateImportReport = true
        };

        MayaSceneData scene;
        MayaImportLog log;

        GameObject root = MayaUnityJsonImporter.ImportJsonIntoScene(
            jsonPath,
            options,
            out scene,
            out log
        );

        Debug.Log("Imported JSON root: " + root.name);
        Debug.Log("Node count: " + scene.Nodes.Count);
        Debug.Log("Connection count: " + scene.Connections.Count);
    }
}
```

---

## Design policy

### Preservation first

Unsupported or partially supported data should not disappear silently. It is kept as scene data, raw attributes, connections, provenance, warnings, or report entries.

### Deterministic reconstruction

The same input should produce stable output where possible. Node order, reconstruction stage, fallback behavior, and report output are designed to be inspectable.

### Baked behavior where appropriate

Constraint behavior is not treated as a full Maya solver reimplementation. The practical game-engine path is to import or preserve baked transform animation and record the constraint source metadata.

### Honest limitations

This project intentionally avoids claiming full Maya compatibility. It is a DCC bridge / validation tool, not a complete substitute for Maya, FBX, or a commercial DCC pipeline.

---

## Current limitations

- `.mb` support is best-effort preservation / recovery, not full binary Maya compatibility.
- Material / shader visual matching is approximate.
- Complex rigs, deformers, references, namespaces, and plugin nodes require more samples.
- BlendShape import is focused on delta vertices; delta normals and delta tangents need further work.
- Constraint runtime solving is not reimplemented; baked transforms are the preferred path.
- Animation tangent / interpolation parity needs more validation.
- Unity compile and runtime validation should be recorded as reproducible reports.

---

## Roadmap

### Short term

- Merge and validate import context cache / `.asmdef` performance branch
- Add import stage profiler and cache hit/miss statistics
- Add more golden exporter JSON samples
- Save validation reports under `Docs/Reports/`
- Add README screenshots / GIFs

### Mid term

- Namespace / reference support
- Texture copy/import pipeline
- More rig / skin / blendshape validation scenes
- Automated Unity Editor tests
- CI regression checks

### Long term

- Batch import UI
- Project-wide DCC validation
- Integration with AssetUtility
- Portfolio-level Technical Art Pipeline Suite documentation

---

## Portfolio wording

> Maya / DCC data can be exported to JSON and reconstructed in Unity with hierarchy, mesh topology, submesh assignment, materials, textures, cameras, lights, animation curves, skin weights, bindposes, blendshapes, constraint metadata, validation reports, and fallback handling. I designed the tool as a preservation-first DCC bridge so unsupported data is still traceable rather than silently discarded.

Avoid these claims:

```text
Maya complete compatibility
Unity official FBX replacement
Perfect reproduction of all Maya scenes
Full Arnold / plugin shader support
Full rig / deformer support
```

Use these claims:

```text
Maya / DCC scene data bridge
Preservation-first Unity importer
Exporter JSON based deterministic reconstruction
Validation-oriented DCC pipeline tool
Constraint baking / Skin / BlendShape aware importer
```
