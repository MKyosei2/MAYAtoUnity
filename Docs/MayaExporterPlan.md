# Maya Exporter Plan

This document defines the next production-oriented direction for MAYAtoUnity.

Direct `.ma` / `.mb` parsing is valuable for preservation, inspection, and research.  
However, a production-grade Maya-to-Unity bridge should also include a Maya-side exporter that writes a stable intermediate format before Unity import.

---

## Why add a Maya-side exporter?

Maya scene files can contain plugin-specific nodes, binary data, animation systems, constraints, references, expressions, shading networks, and deformer state that are difficult to reconstruct perfectly from raw `.ma` / `.mb` alone.

A Maya-side exporter can query Maya through Maya's own scene graph and write the exact subset needed by Unity.

The direct parser remains useful as:

- an audit path,
- a fallback path,
- a source evidence collector,
- and a portfolio demonstration of parser / reconstruction engineering.

The exporter path becomes the production bridge.

---

## Proposed pipeline

```text
Maya scene
  ↓
Maya Python / C++ Exporter
  ↓
MayaUnityScene.json or .assetbundle-like intermediate data
  ↓
Unity Importer
  ↓
Prefab + Mesh + Materials + Animation + Report
```

---

## Intermediate format goals

The intermediate format should be:

- deterministic,
- text-readable at first,
- versioned,
- diffable,
- validated by schema,
- explicit about unsupported data,
- easy for Unity C# to parse.

A JSON format is the fastest first step.

---

## Proposed schema

```json
{
  "schemaVersion": 1,
  "sourceMayaFile": "path/to/scene.ma",
  "sourceHash": "...",
  "units": {
    "linear": "cm",
    "angle": "deg",
    "time": "film"
  },
  "nodes": [],
  "transforms": [],
  "meshes": [],
  "materials": [],
  "textures": [],
  "cameras": [],
  "lights": [],
  "joints": [],
  "skins": [],
  "blendShapes": [],
  "animations": [],
  "constraints": [],
  "unsupported": []
}
```

---

## Export scope v1

| Area | Export fields |
|---|---|
| Transform | name, path, parentPath, localPosition, localRotation, localScale, matrix |
| Mesh | vertices, normals, tangents, UV0, UV1, colors, indices, submeshes |
| Material | name, shader type, color, numeric properties, texture slots |
| Texture | file path, color space hint, usage slot |
| Camera | focal length, FOV, clipping planes, transform |
| Light | type, color, intensity, range, spot angle, transform |
| Joint | hierarchy, bind pose, local transform |
| Skin | joint indices, bone weights, bindposes |
| BlendShape | target names, delta vertices, delta normals, weights |
| Animation | curves, target path, property, keyframes, tangent data if available |
| Constraint | type, target paths, weights, maintain offset flag |
| Unsupported | node name, type, reason |

---

## Unity importer scope v1

The Unity importer should:

1. Read the intermediate JSON.
2. Create deterministic GameObject hierarchy.
3. Generate Mesh assets.
4. Generate Materials.
5. Assign Textures.
6. Create Camera and Light components.
7. Create Animator / AnimationClip where supported.
8. Preserve unsupported source data as components.
9. Write Import Report.

---

## Validation samples

The exporter path should be validated against the same samples listed in `Docs/SupportedNodes.md`.

For each sample, keep:

```text
Samples/<sample-name>/
  source.ma
  exported.json
  unity_result.png
  import_report.md
  expected_counts.md
```

---

## First implementation milestone

The first exporter milestone should focus on:

- Transform hierarchy
- Mesh geometry
- Materials
- Texture file paths
- Cameras
- Lights
- Unsupported node report

Do not start with full rig and animation support.  
Get static asset import stable first.

---

## Second implementation milestone

Add:

- Joints
- Skin weights
- BlendShapes
- Simple transform animation

---

## Third implementation milestone

Add:

- Constraints
- Animation layers
- Reference scene metadata
- Material network expansion
- More robust texture handling

---

## Portfolio positioning

The strongest presentation is:

> Direct `.ma/.mb` parser for audit and preservation, plus a Maya-side exporter for production-grade Unity reconstruction.

This is stronger than claiming raw `.mb` complete parity.
