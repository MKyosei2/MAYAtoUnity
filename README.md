# MAYAtoUnity

**MAYAtoUnity** is a Unity Editor / DCC pipeline tool for importing, preserving, validating, profiling, and reconstructing Maya scene data inside Unity.

This repository is positioned as a **Technical Artist / Tools Programmer portfolio project**. The production problem is simple: teams need to move DCC scene information into a game engine while keeping enough source evidence to debug what was imported, what was approximated, and what is still unsupported.

> Scope note: this is not a replacement for Autodesk Maya, FBX, or Unity's official import pipeline. The defensible claim is an inspectable, deterministic, validation-oriented Maya / DCC bridge for Unity.

---

## Portfolio summary

```text
Maya scene data
  -> Maya-side JSON exporter or Unity-side .ma/.mb preservation path
  -> intermediate MayaSceneData model
  -> deterministic Unity hierarchy reconstruction
  -> mesh / material / texture / camera / light / animation / skin / blendshape attachment
  -> validation log + import report + profile report
```

The main value is not only importing data. The important part is **traceability**: source nodes, attributes, fallback paths, unsupported features, warnings, generated Unity objects, and stage timings are recorded so a reviewer can inspect what happened.

---

## Problem / user / output

| Item | Description |
|---|---|
| Problem | DCC-to-engine transfer can silently lose information, making imported scenes hard to debug. |
| Primary user | Technical Artist, Tools Programmer, Pipeline Engineer, Unity developer. |
| Input | Maya exporter JSON, Maya ASCII `.ma`, best-effort Maya Binary `.mb`. |
| Output | Unity hierarchy, meshes, renderers, materials, cameras, lights, animation clips, skin / blendshape data, reports. |
| Safety goal | Unsupported data should be preserved as evidence instead of disappearing silently. |

---

## 30-second overview

MAYAtoUnity has two import paths.

```text
Path A: Exporter JSON Bridge
Maya scene
  -> Tools/MayaExporter/maya_to_unity_exporter.py
  -> exporter JSON
  -> MayaUnityJsonImporter
  -> Unity hierarchy / mesh / material / animation / skin / blendshape / constraint metadata
  -> import report + validation log + profile report

Path B: Unity-only .ma/.mb preservation
.ma / .mb file
  -> parser / recovery layer
  -> MayaSceneData
  -> UnitySceneBuilder
  -> preserved node records, attributes, connections, provenance, fallback records, and report
```

---

## Reviewer path

For a quick review:

```text
1. Read this README.
2. Open the Unity project.
3. Run: Tools/MAYAtoUnity/Validate All Samples
4. Inspect the generated import reports and logs.
5. Open: Samples/ExporterJson/SimpleMeshMaterialAnimation.json
6. Review: Assets/MayaImporter/MayaUnityJsonImporter.cs
7. Review: Assets/MayaImporter/MayaUnityJsonRuntimeBuilder.cs
8. Review: Docs/ValidationWorkflow.md
```

Expected Unity menu entry points:

```text
Tools/MAYAtoUnity/Validate All Samples
Tools/MAYAtoUnity/Validate Selected Exporter JSON
```

---

## Implemented JSON Bridge support

| Area | Status | Notes |
|---|---:|---|
| Transform hierarchy | Implemented | Builds Unity hierarchy from Maya path / parent path. |
| Mesh topology | Implemented | Reconstructs vertices, normals, UVs, and triangle indices. |
| SubMesh assignment | Implemented | Converts face material assignment into Unity submeshes. |
| Material color | Implemented | Supports lambert / phong / blinn / standardSurface-style base color mapping. |
| Texture binding | Implemented | Maps file texture paths to `_BaseMap` / `_MainTex` where possible. |
| Camera | Implemented | Preserves focal length and clipping plane metadata. |
| Light | Implemented | Directional / point / spot / area fallback support. |
| Animation curves | Implemented | Converts translate / rotate / scale animCurve data into Unity AnimationClip curves. |
| Constraint metadata | Implemented | Stores constraint nodes and bake status in scene data. |
| Constraint baking policy | Implemented | Uses baked transform animation rather than trying to reimplement Maya solvers. |
| Joints | Implemented | Preserves joint hierarchy / jointOrient / matrix metadata. |
| Skin weights | Implemented | Converts top 4 weights per exported vertex into Unity BoneWeight. |
| Bindposes | Implemented | Uses skinCluster bindPreMatrix when available, with joint inverse matrix fallback. |
| SkinnedMeshRenderer | Implemented | Builds skinned renderer when skin data exists. |
| BlendShape deltas | Implemented | Imports sampled delta vertices into Unity blendshape frames. |
| BlendShape weight | Implemented | Applies current weight to SkinnedMeshRenderer. |
| Schema validation | Implemented | Emits warnings / errors before scene conversion. |
| Import profiling | Implemented | Records staged import timing and cache statistics. |
| Import report | Implemented | Writes source evidence, coverage, unsupported entries, and log output. |
| Validation menu | Implemented | Editor menu for sample import and report generation. |

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

Preserved data includes node name, node type, parent hierarchy, UUID, raw attributes, typed parsed attributes, plug connections, raw command statements, and unit information.

### `.mb` path

`.mb` is handled as best-effort preservation / recovery rather than full Maya Binary compatibility.

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
| `AsciiCommands` | `.ma` command or exporter JSON source. |
| `MbEmbeddedAscii` | Command-like text found inside `.mb`. |
| `MbNullTerminatedAscii` | Recovered from null-terminated strings. |
| `MbDeterministicStringTable` | Deterministic enumeration from string table. |
| `MbChunkPlaceholder` | Placeholder from chunk index. |
| `MbHeuristic` | Heuristic reconstruction. |

---

## Architecture

```text
Assets/MayaImporter/
  MayaImporter.cs                    # public import entry point
  MayaImportOptions.cs               # import configuration
  MayaImportReport.cs                # Markdown report writer
  MayaUnityJsonImporter.cs           # JSON bridge entry point, schema validation, profiling
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

Unsupported or partially supported data should not disappear silently. It is kept as scene data, raw attributes, connections, provenance, warnings, unsupported feature entries, or report output.

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
- More committed sample reports, screenshots, and GIFs would improve portfolio review speed.

---

## Next improvements

### Short term

- Add committed sample reports under `Docs/Reports/`.
- Add README screenshots / GIFs showing import before and after.
- Add more golden exporter JSON samples for skin, blendshape, animation, and constraints.
- Record real import-stage timings from Unity validation runs.

### Mid term

- Namespace / reference support.
- Texture copy/import pipeline.
- More rig / skin / blendshape validation scenes.
- Automated Unity Editor tests.
- CI compile / regression checks where possible.

### Long term

- Batch import UI.
- Project-wide DCC validation.
- Integration with AssetUtility.
- Portfolio-level Technical Art Pipeline Suite documentation.

---

## Portfolio wording

> Maya / DCC data can be exported to JSON and reconstructed in Unity with hierarchy, mesh topology, submesh assignment, materials, textures, cameras, lights, animation curves, skin weights, bindposes, blendshapes, constraint metadata, validation reports, profiling, and fallback handling. I designed the tool as a preservation-first DCC bridge so unsupported data remains traceable instead of silently disappearing.

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
Profiled import and report workflow
```
