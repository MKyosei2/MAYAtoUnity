# Exporter JSON Sample Expected Report Statistics

This file defines expected validation evidence for `Samples/ExporterJson/SimpleMeshMaterialAnimation.json`.

Use this together with:

```text
Tools > MAYAtoUnity > Validate Selected Exporter JSON
```

or:

```text
Tools > MAYAtoUnity > Validate All Samples
```

Generated reports are written under:

```text
Assets/MayaImported/Reports
```

---

## Sample

```text
Samples/ExporterJson/SimpleMeshMaterialAnimation.json
```

## Expected bridge coverage

| Feature | Expected evidence |
|---|---:|
| Exporter JSON schemaVersion | 9 |
| Transform nodes | at least 4 |
| Mesh nodes | at least 1 |
| Total subMeshes | at least 2 |
| Materials | at least 2 |
| Cameras | at least 1 |
| Lights | at least 1 |
| BlendShape-enabled meshes | at least 1 |
| Total BlendShape targets | at least 1 |
| JSON animation curves | at least 1 |
| Constraint-baked animation curves | at least 1 |
| Constraint metadata nodes | at least 1 |

---

## Expected Unity reconstruction behavior

After import, the Unity scene should contain:

- A root GameObject generated from the JSON source.
- A `JsonMesh` object or equivalent reconstructed target transform.
- A Unity `Mesh` built from JSON vertices / normals / uvs / indices.
- Two subMesh material slots using `JsonRed` and `JsonBlue` order.
- A `SkinnedMeshRenderer` for the BlendShape-only mesh path.
- A BlendShape named `RaiseCorner` with current weight applied.
- A Camera component.
- A Light component.
- A legacy AnimationClip with a curve for `localPosition.y`.
- A constraint metadata node recorded as baked to animation.

---

## Notes

- This is not a visual parity test against Maya.
- It is a deterministic bridge validation sample.
- Constraint behavior is intentionally baked to animation curves rather than solved live in Unity.
- Material color is expected to be approximate; shader parity is not claimed.
- BlendShape validation focuses on `deltaVertices`; normals/tangents are future work.
