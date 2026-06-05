# MAYAtoUnity Supported Nodes and Validation Scope

This document defines the practical validation scope for MAYAtoUnity.

The goal of this project is not to claim complete behavioral parity with Autodesk Maya.  
The goal is to make Maya scene data inspectable, auditable, and reconstructable inside Unity for a clearly defined set of game-development pipeline use cases.

---

## Support levels

| Level | Meaning |
|---|---|
| Preserved | Source node / attribute / connection is kept in `MayaSceneData` and visible in Unity components. |
| Reconstructed | A Unity GameObject / Component / Asset is generated from the source data. |
| Behavior | Runtime behavior is simulated or approximated in Unity. |
| Reported | Unsupported or partial data is explicitly listed in an import report. |

---

## Current target scope

| Area | Target level | Notes |
|---|---|---|
| Transform hierarchy | Reconstructed | Parent-child hierarchy, transform-like nodes, deterministic build order. |
| Node identity | Preserved | Name, type, parent, UUID, provenance. |
| Raw attributes | Preserved | Raw tokens and typed parsed values where possible. |
| Connections | Preserved | Source and destination plugs are stored. |
| Mesh nodes | Preserved / Partial reconstruction | Geometry parity must be validated per sample. |
| Materials | Preserved / Partial reconstruction | Maya shader parity is not guaranteed. |
| Textures | Preserved / Partial reconstruction | File and texture-like nodes are detected. |
| Cameras | Reconstructed where supported | Requires Unity component mapping. |
| Lights | Reconstructed where supported | Requires Unity component mapping. |
| Joints | Preserved / Partial reconstruction | Transform hierarchy is preserved; skinning requires validation. |
| SkinCluster | Preserved / Partial behavior | Runtime repair can help but not full Maya evaluation. |
| BlendShape | Preserved / Partial behavior | Requires mesh target validation. |
| Constraints | Preserved / Partial behavior | Runtime auto-bind supports simple cases. |
| Animation curves | Preserved / Experimental | Command records exist; clip generation requires validation. |
| Expressions / scriptNode | Reported only | Preserved as source evidence, not executed. |
| Maya plugins | Reported only | `requires` records are kept. |
| Unknown nodes | Preserved / Reported | Stored as generic or unknown components. |

---

## Validation samples to maintain

| Sample | Required proof |
|---|---|
| `SimpleHierarchy.ma` | DAG hierarchy, transform node count, parent links. |
| `MaterialTexture.ma` | material nodes, file texture nodes, shadingEngine connections. |
| `CameraLight.ma` | camera and light nodes reconstructed in Unity. |
| `TransformAnimation.ma` | setKeyframe / animCurve data preserved and reported. |
| `ConstraintSample.ma` | constraint nodes preserved and runtime auto-bind reviewed. |
| `SkinBlendShape.ma` | joint, skinCluster, blendShape nodes preserved and reported. |
| `UnsupportedNodes.ma` | unsupported nodes appear in Import Report. |
| `BinaryRecovery.mb` | raw binary SHA, string-table evidence, provenance breakdown. |

---

## Definition of done for each sample

A validation sample is considered acceptable only when it has:

1. Source `.ma` or `.mb` file.
2. Generated Unity hierarchy screenshot.
3. Generated Import Report markdown.
4. Expected node / connection counts documented.
5. Unsupported nodes explicitly listed.
6. Known limitations written next to the sample.

---

## Claims that should not be made

Do not claim:

- Full Maya behavioral compatibility.
- Full `.mb` binary format parity.
- Full Arnold / plugin shader parity.
- Full rig / deformer / animation evaluation parity.
- Replacement for Unity's official FBX workflow.

Preferred claim:

> MAYAtoUnity preserves Maya node, attribute, connection, hierarchy, and recovery evidence in a Unity-inspectable data model, then reconstructs a deterministic Unity hierarchy for a defined validation set.
