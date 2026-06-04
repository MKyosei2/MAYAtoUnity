# MAYAtoUnity Production Standard

This document defines the minimum bar required for MAYAtoUnity to become a main portfolio project for top-tier game company applications.

The requested target is extremely high:

> Read everything that exists in Maya and bring it into Unity exactly as it is.

That target cannot be achieved by a small custom parser alone. Maya is a full DCC application with its own dependency graph, node evaluation, deformers, constraints, animation systems, shading networks, plugins, scene units, references, namespaces, and binary file behavior.

Therefore, the production strategy must be changed from:

> Custom parser tries to perfectly understand all Maya data.

To:

> Hybrid production pipeline that uses official / stable interchange formats where necessary, and uses custom parsing for audit, metadata, provenance, validation, and Unity-side reconstruction support.

---

## 1. Final target

MAYAtoUnity should become a DCC-to-Unity import and validation pipeline.

The final target is:

- Import Maya-authored assets into Unity with high fidelity.
- Preserve Maya node / attribute / connection metadata where useful.
- Use robust interchange paths for geometry, animation, materials, cameras, and scene hierarchy.
- Provide validation reports that prove what was imported, what was approximated, and what was unsupported.
- Support repeatable batch import for production-like workflows.

The tool should be presented as:

> A Unity-side DCC pipeline tool that imports Maya-authored scene data through a hybrid FBX / USD / Alembic / custom-metadata workflow, preserving raw Maya node evidence and generating validation reports inside Unity.

It should not be presented as:

> A fully custom Maya binary reader that perfectly recreates every Maya feature.

---

## 2. Reality check: what “complete Maya import” requires

To carry Maya data into Unity at production level, the pipeline must handle at least the following categories.

| Category | Required strategy |
|---|---|
| Transform hierarchy | Import through FBX / USD and validate with custom metadata |
| Mesh geometry | Import through FBX / USD / Alembic |
| UVs | Import through FBX / USD |
| Normals / tangents | Import through FBX / Unity importer settings |
| Materials | Map Maya shading networks to Unity materials or store unsupported nodes |
| Textures | Resolve file paths and copy / remap assets |
| Skinning | Import through FBX where possible |
| Blend shapes | Import through FBX where possible |
| Animation curves | Import through FBX or baked animation clips |
| Cameras | Import through FBX / custom mapping |
| Lights | Import through FBX / custom mapping |
| Constraints | Bake to animation or recreate supported constraints |
| Deformers | Bake to mesh cache / Alembic where necessary |
| References | Resolve during Maya-side export or record unresolved references |
| Namespaces | Preserve names and original paths |
| Custom attributes | Export metadata JSON and attach to Unity components |
| Plugin nodes | Store as unsupported metadata unless a plugin adapter exists |
| Maya binary `.mb` | Use Maya-side export step or treat custom parser as audit-only |

A pure Unity-side `.mb` parser should remain experimental / audit-oriented unless it can be validated against a large suite of files.

---

## 3. Required architecture upgrade

The current custom parser is useful, but it should become one layer of a larger pipeline.

Target architecture:

```text
Maya Scene
  ↓
Maya-side exporter / interchange export
  ↓
+------------------+-------------------+-------------------+
| FBX              | USD               | Alembic           |
| mesh / skin      | scene hierarchy   | baked cache       |
| anim / blend     | metadata-friendly | deformation cache |
+------------------+-------------------+-------------------+
  ↓
Unity Importer Layer
  ↓
Imported Unity assets
  ↓
Custom Metadata Importer
  ↓
MayaSceneData / NodeRecord / ConnectionRecord
  ↓
Validation Report
  ↓
Runtime / Editor reconstruction helpers
```

The existing `.ma` / `.mb` parser should be used for:

- Raw command audit.
- Node metadata extraction.
- Attribute preservation.
- Connection preservation.
- Unsupported node reporting.
- Debug / validation comparison.

It should not be the only path for high-fidelity mesh / rig / animation transfer.

---

## 4. Non-negotiable minimum requirements

The project is not main-project ready until the following are true.

### 4.1 Sample import suite

The repository must include or document reproducible samples:

```text
samples/
  01_transform_hierarchy/
    source.ma
    exported.fbx
    metadata.json
    expected_report.json

  02_static_mesh_material/
    source.ma
    exported.fbx
    textures/
    expected_report.json

  03_skinned_mesh/
    source.ma
    exported.fbx
    expected_report.json

  04_blendshape/
    source.ma
    exported.fbx
    expected_report.json

  05_animation/
    source.ma
    exported.fbx
    expected_report.json

  06_constraint_baked/
    source.ma
    exported.fbx
    expected_report.json

  07_unsupported_plugin_node/
    source.ma
    metadata.json
    expected_report.json
```

### 4.2 Import report

Every import must generate a report:

```json
{
  "sourceFile": "character.ma",
  "sourceHash": "...",
  "importMode": "fbx_plus_metadata",
  "nodesParsed": 152,
  "connectionsParsed": 304,
  "gameObjectsCreated": 98,
  "meshesImported": 12,
  "materialsCreated": 8,
  "texturesResolved": 14,
  "animationsImported": 3,
  "blendShapesImported": 6,
  "constraintsBaked": 4,
  "unsupportedNodes": [
    { "name": "customNode1", "type": "pluginNode", "reason": "No adapter" }
  ],
  "warnings": [],
  "errors": [],
  "durationMs": 842
}
```

### 4.3 Unity verification scene

The repository must include a Unity verification scene that shows:

- Imported hierarchy.
- Imported mesh.
- Imported material / texture.
- Imported animation.
- Metadata inspector component.
- Report output.

### 4.4 Supported feature matrix

The README must include a strict matrix:

| Maya feature | Supported | Import path | Notes |
|---|---:|---|---|
| Transform | Yes | FBX / custom metadata | Validated sample required |
| Mesh | Yes | FBX | Validated sample required |
| Material | Partial | Custom mapping | Supported shader list required |
| Skinning | Yes / Partial | FBX | Validated sample required |
| BlendShape | Yes / Partial | FBX | Validated sample required |
| Constraint | Baked | Maya-side export | Runtime constraint recreation optional |
| Deformer | Baked | Alembic / FBX | Exact Maya eval not required |
| Plugin node | Metadata only | JSON / custom parser | Adapter required for behavior |

### 4.5 Tests

Minimum tests:

```text
Tests/
  MayaAsciiParserTests.cs
  MetadataImportTests.cs
  ConnectionParserTests.cs
  ImportReportTests.cs
  UnityHierarchyValidationTests.cs
```

Required test cases:

- `createNode` parsing.
- `setAttr` parsing.
- `connectAttr` parsing.
- parent hierarchy reconstruction.
- unsupported node preservation.
- report generation.
- deterministic import result.

---

## 5. Feature upgrade roadmap

### Phase 1: Make current parser trustworthy

- Add sample `.ma` files.
- Add parser unit tests.
- Add import reports.
- Add supported node matrix.
- Add unsupported node report.
- Add deterministic output tests.

Acceptance criteria:

- `SimpleHierarchy.ma` imports consistently.
- Node count / connection count matches expected report.
- Unsupported nodes are preserved, not lost.

### Phase 2: Build hybrid FBX + metadata workflow

- Add Maya-side metadata exporter script.
- Export FBX for geometry / animation.
- Export JSON for node metadata / custom attributes / connections.
- Unity importer reads FBX output and attaches metadata components.
- Add sample scene verification.

Acceptance criteria:

- Static mesh + material sample imports into Unity.
- Metadata component shows original Maya node data.
- Report lists imported and unsupported features.

### Phase 3: Add rig / animation coverage

- Validate skinned mesh import.
- Validate blend shape import.
- Validate animation clip import.
- Bake constraints from Maya side when exact runtime recreation is not possible.
- Record constraint metadata.

Acceptance criteria:

- Character sample imports with skeleton, skinning, animation, and metadata.
- Unity verification scene shows animation playback.

### Phase 4: Add USD / Alembic path

For high-fidelity scene and cache workflows:

- Add USD path for hierarchy / metadata-heavy scene transfer.
- Add Alembic path for baked deformation / cache transfer.
- Add import report comparison across FBX / USD / Alembic.

Acceptance criteria:

- Deforming mesh cache sample imports through Alembic.
- Scene hierarchy / metadata sample imports through USD path or documented workflow.

### Phase 5: Main portfolio release

- Add demo GIF.
- Add sample assets.
- Add architecture diagram.
- Add validation reports.
- Add test results.
- Add known limitations.
- Add release package.

---

## 6. Required modules

Target production structure:

```text
Assets/MayaImporter/
  Editor/
    MayaImportWindow.cs
    MayaBatchImportWindow.cs
    ImportReportViewer.cs

  Import/
    MayaImportPipeline.cs
    FbxImportCoordinator.cs
    MetadataImporter.cs
    UnityAssetBinder.cs

  Parsing/
    MayaAsciiParser.cs
    MayaBinaryAuditParser.cs
    MayaSetAttrValueParser.cs
    MayaConnectionParser.cs

  Data/
    MayaSceneData.cs
    NodeRecord.cs
    ConnectionRecord.cs
    RawAttributeValue.cs
    ImportReport.cs
    SupportedFeatureMatrix.cs

  Runtime/
    MayaNodeComponentBase.cs
    MayaMetadataComponent.cs
    MayaRuntimeAutoFix.cs
    MayaRuntimeAutoBind.cs

  Validation/
    ImportValidator.cs
    HierarchyValidator.cs
    MeshValidator.cs
    AnimationValidator.cs

  Tests/
    Editor/
    Runtime/
```

---

## 7. Portfolio wording rule

Allowed wording after Phase 2:

> Maya-authored assetsをUnityへ取り込むDCC pipeline toolを開発。FBXによるmesh / animation importと、独自metadata importerによるMaya node / attribute / connection preservationを組み合わせ、import reportと検証sceneで結果を確認できるようにした。

Allowed wording for current custom parser:

> Maya `.ma/.mb`由来のnode / attribute / connectionをUnity側で解析し、raw dataとprovenanceを保持するpreservation-first parserを実装した。

Forbidden wording until full hybrid validation exists:

> Mayaにあるものを全て完全にUnityへそのまま持ってこられます。

---

## 8. Main-project readiness checklist

MAYAtoUnity can become a main portfolio project only when this checklist is complete.

- [ ] Sample `.ma` files exist.
- [ ] FBX + metadata hybrid path exists.
- [ ] Unity verification scene exists.
- [ ] Import report is generated.
- [ ] Parser unit tests exist.
- [ ] Supported feature matrix exists.
- [ ] Unsupported node report exists.
- [ ] Static mesh sample validated.
- [ ] Material / texture sample validated.
- [ ] Skinned mesh sample validated.
- [ ] BlendShape sample validated.
- [ ] Animation sample validated.
- [ ] Constraint bake sample validated.
- [ ] README has GIF.
- [ ] README has screenshots.
- [ ] README does not overclaim full Maya compatibility.
