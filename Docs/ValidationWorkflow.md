# MAYAtoUnity Validation Workflow

This document explains how to validate MAYAtoUnity as a portfolio-grade DCC pipeline tool.

MAYAtoUnity currently has two validation paths:

1. Unity-only `.ma/.mb` preservation / recovery validation
2. Maya Exporter JSON Bridge validation

---

## What is validated

| Area | Validation artifact |
|---|---|
| Import audit | `Assets/MayaImporter/MayaImportReport.cs` |
| Report options | `MayaImportOptions.GenerateImportReport` and report-related options |
| Sample validation menu | `Tools/MAYAtoUnity/Validate All Samples` |
| Single Maya file validation | `Tools/MAYAtoUnity/Validate Selected Maya File` |
| Single exporter JSON validation | `Tools/MAYAtoUnity/Validate Selected Exporter JSON` |
| Supported scope document | `Docs/SupportedNodes.md` |
| Maya exporter plan | `Docs/MayaExporterPlan.md` |
| Maya exporter implementation | `Tools/MayaExporter/maya_to_unity_exporter.py` |
| Maya ASCII samples | `Samples/SimpleHierarchy.ma`, `Samples/MaterialTexture.ma`, `Samples/CameraLight.ma`, `Samples/TransformAnimation.ma`, `Samples/ConstraintSample.ma` |
| Exporter JSON sample | `Samples/ExporterJson/SimpleMeshMaterialAnimation.json` |
| Expected JSON report stats | `Samples/ExporterJson/ExpectedReportStats.md` |

---

## How to run all sample validation

1. Open the repository as a Unity project.
2. Wait for scripts to compile.
3. Open:

```text
Tools > MAYAtoUnity > Validate All Samples
```

This scans `Samples/` for:

```text
.ma
.mb
.json
```

Reports are generated under:

```text
Assets/MayaImported/Reports
```

Review each report for:

- node count
- connection count
- node type breakdown
- provenance breakdown
- JSON Bridge overview, when the source is exporter JSON
- unsupported / generic-risk nodes
- warnings
- errors
- validation checklist results

---

## How to validate one Maya file

1. Select a `.ma` or `.mb` file under `Assets/`.
2. Open:

```text
Tools > MAYAtoUnity > Validate Selected Maya File
```

3. Inspect the generated hierarchy and report.

This path validates the Unity-only preservation / recovery importer.

---

## How to validate one exporter JSON file

1. Select an exporter `.json` file under `Assets/` or a project-visible folder.
2. Open:

```text
Tools > MAYAtoUnity > Validate Selected Exporter JSON
```

Recommended sample:

```text
Samples/ExporterJson/SimpleMeshMaterialAnimation.json
```

Expected report stats:

```text
Samples/ExporterJson/ExpectedReportStats.md
```

This path validates the JSON Bridge importer.

The sample includes:

- Transform hierarchy
- Mesh topology
- Normals
- UVs
- SubMesh assignment
- Face material assignment
- Materials
- Camera
- Light
- BlendShape deltaVertices
- Current BlendShape weight
- Baked constraint-style animation curve
- Constraint metadata

---

## JSON Bridge validation expectations

When validating `Samples/ExporterJson/SimpleMeshMaterialAnimation.json`, the importer should exercise these systems:

| System | Expected behavior |
|---|---|
| JSON parse | `MayaUnityExport` DTO is created via `JsonUtility` |
| SceneData conversion | Nodes, transforms, mesh metadata, materials, animation curves, constraints are recorded |
| Unity hierarchy | `JsonRoot`, `JsonMesh`, camera and light objects are generated |
| Mesh build | Unity `Mesh` is built from vertices / normals / uvs / indices |
| SubMesh build | Two subMeshes are created from material-specific index lists |
| Material assignment | `JsonRed` and `JsonBlue` are assigned in subMesh order |
| BlendShape | `RaiseCorner` frame is added and current weight is applied |
| Animation | `bakedConstraint.translateY` curve becomes a Unity AnimationClip curve |
| Constraint trace | `sampleParentConstraint` is recorded as baked to animation |
| Report | Markdown report includes a JSON Bridge overview table |

---

## Report interpretation

The report separates source discovery from Unity behavior parity.

| Report item | Meaning |
|---|---|
| Summary | Source path, source kind, schema version, root object, node/log counts. |
| Source evidence | SHA-256, raw ASCII/binary evidence, `.mb` recovery evidence. |
| JSON Bridge overview | Exporter schema, mesh/subMesh/material/skin/blendshape/animation/constraint counts for JSON sources. |
| Coverage overview | Structural feature-family counts. |
| Node type breakdown | Which Maya node families were discovered. |
| Provenance breakdown | How each node was recovered, especially for `.mb`. |
| Unsupported nodes | Nodes requiring manual review or future implementation. |
| Import log | Runtime warnings/errors emitted during parse/build/attachment. |
| Validation checklist | Basic proof that the import produced auditable data. |

---

## Validation gates before portfolio submission

Do not present this as a main portfolio project until the following are true:

- [ ] `Validate All Samples` runs without compiler errors.
- [ ] Each sample produces an Import Report.
- [ ] Exporter JSON sample imports without errors.
- [ ] Exporter JSON report matches `Samples/ExporterJson/ExpectedReportStats.md`.
- [ ] Mesh sample shows visible geometry in Unity.
- [ ] SubMesh sample shows multiple material slots.
- [ ] BlendShape sample creates a SkinnedMeshRenderer and applies current weight.
- [ ] Animation sample creates an AnimationClip or visible transform animation.
- [ ] Constraint sample clearly states that behavior is baked, not solver-simulated.
- [ ] Each sample has a screenshot of the Unity hierarchy.
- [ ] Each sample has expected node / connection counts documented.
- [ ] Unsupported nodes are not hidden.
- [ ] README links to sample reports and screenshots.
- [ ] A clear limitation statement is visible.

---

## Implementation gates

### Gate 1: Static scene bridge

- Transform hierarchy
- Material metadata
- Texture paths
- Camera and light metadata
- Import reports

### Gate 2: Geometry bridge

- Maya exporter writes vertices, normals, UVs, indices.
- Unity importer converts exported mesh data into Unity Mesh.
- SubMesh material mapping is preserved.
- Mesh sample has before / after screenshot.

### Gate 3: Rig / animation bridge

- Joints
- Bindposes
- Skin weights
- SkinnedMeshRenderer
- BlendShape delta vertices
- Current BlendShape weight
- Transform animation
- Constraint bake to animation curves

### Gate 4: Production polish

- Editor Import Window
- Batch import
- Report comparison
- Golden sample tests
- CI parser checks
- Real Maya scene validation set

---

## Known validation risks

Current risks to verify in Unity:

- Matrix handedness and bindpose orientation need real rig validation.
- BlendShape extraction currently focuses on `deltaVertices`; normals and tangents are future work.
- Constraint bake uses frame sampling, not Unity-side solver recreation.
- JSON paths for external textures depend on whether paths are inside the Unity project.
- Material/shader appearance is approximate and must be visually reviewed.

---

## Honest portfolio wording

Use this:

> MAYAtoUnity is a preservation-first Maya-to-Unity pipeline tool. It includes a Unity-only `.ma/.mb` audit path and a Maya-side JSON exporter path. The JSON Bridge reconstructs hierarchy, mesh topology, subMeshes, materials, cameras, lights, animation curves, constraint-baked transforms, skin weights, bindposes, and blendshape deltas into Unity, while import reports preserve evidence and limitations.

Do not use this:

> Fully imports every Maya feature into Unity.

Also avoid:

```text
Maya完全互換
Unity公式FBXの代替
あらゆるMayaシーンを完全再現
全shader / all rig / all deformer完全対応
```
