# MAYAtoUnity Validation Workflow

This document explains how to validate MAYAtoUnity as a portfolio-grade DCC pipeline tool.

---

## What was added for validation

| Area | Added artifact |
|---|---|
| Import audit | `Assets/MayaImporter/MayaImportReport.cs` |
| Report options | `MayaImportOptions.GenerateImportReport` and report-related options |
| Builder fix | `MayaImporter.ImportIntoScene` now calls `UnitySceneBuilder` |
| Sample validation menu | `Tools/MAYAtoUnity/Validate All Samples` |
| Single file validation menu | `Tools/MAYAtoUnity/Validate Selected Maya File` |
| Supported scope document | `Docs/SupportedNodes.md` |
| Maya exporter plan | `Docs/MayaExporterPlan.md` |
| Maya exporter prototype | `Tools/MayaExporter/maya_to_unity_exporter.py` |
| Sample `.ma` files | `Samples/SimpleHierarchy.ma`, `Samples/MaterialTexture.ma` |

---

## How to run sample validation

1. Open the repository as a Unity project.
2. Wait for scripts to compile.
3. Open:

```text
Tools > MAYAtoUnity > Validate All Samples
```

4. Reports will be generated under:

```text
Assets/MayaImported/Reports
```

5. Review each report for:

- node count,
- connection count,
- node type breakdown,
- provenance breakdown,
- unsupported / generic-risk nodes,
- warnings,
- errors,
- validation checklist results.

---

## How to validate one file

1. Select a `.ma` or `.mb` file under `Assets/`.
2. Open:

```text
Tools > MAYAtoUnity > Validate Selected Maya File
```

3. Inspect the generated hierarchy and report.

---

## Report interpretation

The report separates source discovery from Unity behavior parity.

| Report item | Meaning |
|---|---|
| Node count | Source nodes preserved in `MayaSceneData`. |
| Connection count | Maya plug connections preserved. |
| Node type breakdown | Which Maya node families were discovered. |
| Provenance breakdown | How each node was recovered, especially for `.mb`. |
| Unsupported nodes | Nodes requiring manual review or future implementation. |
| Validation checklist | Basic proof that the import produced auditable data. |

---

## Validation gates before portfolio submission

Do not present this as a main portfolio project until the following are true:

- [ ] `Validate All Samples` runs without compiler errors.
- [ ] Each sample produces an Import Report.
- [ ] Each sample has a screenshot of the Unity hierarchy.
- [ ] Each sample has expected node / connection counts documented.
- [ ] Unsupported nodes are not hidden.
- [ ] README links to sample reports and screenshots.
- [ ] At least one material / texture sample is shown.
- [ ] At least one camera / light sample is shown.
- [ ] At least one animation or constraint sample is shown, even if partial.
- [ ] A clear limitation statement is visible.

---

## Next implementation gates

### Gate 1: Static scene bridge

- Transform hierarchy
- Mesh metadata
- Material metadata
- Texture paths
- Camera and light metadata
- Import reports

### Gate 2: Geometry bridge

- Maya exporter writes vertices, normals, UVs, indices, submesh material mapping.
- Unity importer converts exported mesh data into Unity Mesh assets.
- Mesh sample has before / after screenshot.

### Gate 3: Rig / animation bridge

- Joints
- Bindposes
- Skin weights
- BlendShape targets
- Simple transform animation
- AnimationClip generation

### Gate 4: Production polish

- Editor Import Window
- Batch import
- Report comparison
- Golden sample tests
- CI parser checks

---

## Honest portfolio wording

Use this:

> MAYAtoUnity is a preservation-first Maya-to-Unity pipeline tool. It parses `.ma` / `.mb` evidence directly for auditability and also includes the foundation for a Maya-side exporter path. Import results are reconstructed as Unity GameObjects and reported with node counts, connection counts, provenance, unsupported nodes, and validation checklists.

Do not use this yet:

> Fully imports every Maya feature into Unity.
