# MAYAtoUnity Supported Nodes and Validation Scope

This document defines the practical validation scope for MAYAtoUnity.

The goal of this project is not to claim complete behavioral parity with Autodesk Maya.  
The goal is to make Maya / DCC scene data inspectable, auditable, and reconstructable inside Unity for a clearly defined set of game-development pipeline use cases.

---

## Support levels

| Level | Meaning |
|---|---|
| Preserved | Source node / attribute / connection is kept in `MayaSceneData` and visible in Unity components or reports. |
| Reconstructed | A Unity GameObject / Component / Asset is generated from the source data. |
| Baked | Maya-side evaluated behavior is sampled and converted to Unity data such as AnimationCurve. |
| Approximated | Unity representation is useful but not behavior-identical to Maya. |
| Reported | Unsupported or partial data is explicitly listed in an import report. |

---

## Current target scope

### JSON Bridge path

This path uses:

```text
Tools/MayaExporter/maya_to_unity_exporter.py
  ↓
Exporter JSON schema v9
  ↓
MayaUnityJsonImporter
  ↓
Unity reconstruction
```

| Area | Target level | Notes |
|---|---|---|
| Transform hierarchy | Reconstructed | Maya DAG path / parentPath are mapped to Unity hierarchy. |
| Node identity | Preserved | name, type, parent, UUID, provenance. |
| Units | Preserved | linear / angle / time unit metadata. |
| Mesh topology | Reconstructed | vertices, normals, UVs, triangle indices. |
| SubMesh | Reconstructed | face material assignment becomes Unity subMesh index lists. |
| Materials | Reconstructed / Approximated | base color and fallback shader mapping; full shader parity is not claimed. |
| Texture paths | Reconstructed where available | Unity AssetDatabase path or file path is used when loadable. |
| Cameras | Reconstructed / Approximated | Camera component is created; focal length is applied when Unity API supports it. |
| Lights | Reconstructed / Approximated | directional / point / spot / area metadata; area light falls back when unavailable. |
| Native animation curves | Reconstructed | translate / rotate / scale curves become legacy AnimationClip curves. |
| Constraints | Baked / Preserved | parent / point / orient / scale / aim constraints are sampled as transform animation; metadata is retained. |
| Joints | Reconstructed / Preserved | joint hierarchy and jointOrient metadata retained. |
| Skin weights | Reconstructed | top 4 weights per exported vertex become Unity BoneWeight. |
| Bindposes | Reconstructed / Fallback | skinCluster.bindPreMatrix is preferred; inverse joint matrix fallback is used. |
| BlendShape deltaVertices | Reconstructed | target deltas are sampled in Maya and added via AddBlendShapeFrame. |
| BlendShape currentWeight | Reconstructed | current Maya weight is applied to SkinnedMeshRenderer. |
| Import report | Reported | JSON Bridge statistics and validation checklist are written to Markdown. |

### Unity-only `.ma/.mb` preservation path

This path uses Unity C# parsers and does not require Maya / Autodesk APIs.

| Area | Target level | Notes |
|---|---|---|
| `.ma` createNode | Preserved / Reconstructed | NodeRecord and GameObject hierarchy are generated. |
| `.ma` setAttr | Preserved | raw tokens plus typed parsed values where possible. |
| `.ma` connectAttr | Preserved | source / destination plug records. |
| `.ma` parent / currentUnit | Preserved / Reconstructed | hierarchy and unit metadata. |
| `.ma` fileInfo / requires / workspace | Preserved / Reported | metadata retained for audit. |
| `.ma` namespace / addAttr / deleteAttr / lockNode | Preserved / Reported | structured records or node attributes. |
| `.ma` setKeyframe / animLayer / expression / scriptNode | Preserved / Reported | not full Maya evaluation. |
| `.ma` particle / field nodes | Preserved / Reported | particle, emitter, gravityField, turbulenceField, and related connections are source evidence; Unity simulation parity is not claimed. |
| `.ma` rigid-body nodes | Preserved / Reported | rigidBody / rigidSolver nodes and attributes are retained as physics metadata where present. |
| `.ma` nDynamics nodes | Preserved / Reported | nucleus, nCloth, nRigid, dynamicConstraint nodes are retained as simulation metadata where present. |
| `.mb` raw binary | Preserved | bytes and SHA-256 retained. |
| `.mb` embedded ASCII | Preserved / Recovered | command-like text parsed when confidence is sufficient. |
| `.mb` null-terminated strings | Preserved / Recovered | best-effort statement reconstruction. |
| `.mb` string table | Preserved / Recovered | deterministic node enumeration and hints. |
| `.mb` placeholders | Reported | fallback nodes are clearly marked by provenance. |

---

## Validation samples to maintain

| Sample | Required proof |
|---|---|
| `Samples/SimpleHierarchy.ma` | DAG hierarchy, transform node count, parent links. |
| `Samples/MaterialTexture.ma` | material nodes, file texture nodes, shadingEngine connections. |
| `Samples/CameraLight.ma` | camera and light nodes reconstructed or reported. |
| `Samples/TransformAnimation.ma` | setKeyframe / animCurve data preserved and reported. |
| `Samples/ConstraintSample.ma` | constraint nodes preserved and limitation clearly reported. |
| `Samples/FxPhysicsShowcase.ma` | 3D model hierarchy, particles, fields, rigidBody metadata, nDynamics nodes, setKeyframe evidence, and unsupported simulation reporting. |
| `Samples/ExporterJson/SimpleMeshMaterialAnimation.json` | JSON Bridge path: mesh, subMesh, materials, blendshape, animation, constraint metadata. |
| Future `SkinBlendShape.json` | joint, skinCluster, bindposes, blendShape targets and current weights. |
| Future `UnsupportedNodes.ma` | unsupported nodes appear in Import Report. |
| Future `BinaryRecovery.mb` | raw binary SHA, string-table evidence, provenance breakdown. |

---

## Definition of done for each sample

A validation sample is considered acceptable only when it has:

1. Source `.ma`, `.mb`, or exporter `.json` file.
2. Generated Unity hierarchy screenshot.
3. Generated Import Report markdown.
4. Expected node / connection / JSON Bridge statistics documented.
5. Unsupported nodes explicitly listed.
6. Known limitations written next to the sample.
7. For JSON Bridge samples, the report must show relevant feature counts such as mesh, subMesh, BlendShape, animation, and constraint-bake counts.

---

## Claims that should not be made

Do not claim:

- Full Maya behavioral compatibility.
- Full `.mb` binary format parity.
- Full Arnold / plugin shader parity.
- Full rig / deformer / animation evaluation parity.
- Constraint solver parity in Unity.
- Particle, rigid-body, or nCloth simulation parity in Unity.
- Replacement for Unity's official FBX workflow.

Preferred claim:

> MAYAtoUnity is a preservation-first Maya-to-Unity pipeline tool. It includes a Unity-only `.ma/.mb` audit path and a Maya-side JSON exporter path. The JSON Bridge reconstructs a defined validation set covering hierarchy, mesh topology, subMeshes, materials, cameras, lights, animation curves, baked constraints, skin weights, bindposes, and blendshape deltas while preserving import evidence in reports. The `.ma/.mb` audit path preserves additional DCC evidence such as unsupported particles, fields, rigid-body nodes, and nDynamics metadata instead of silently discarding it.
