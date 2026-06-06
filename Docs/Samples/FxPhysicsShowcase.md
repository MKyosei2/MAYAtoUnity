# FxPhysicsShowcase Maya Sample

`Samples/FxPhysicsShowcase.ma` is a MAYAtoUnity validation / portfolio sample focused on **3D model hierarchy + particles + physics node preservation**.

This sample is intentionally designed for the Unity-only `.ma` audit path. It should demonstrate that MAYAtoUnity can preserve scene evidence even when Unity reconstruction is not expected to simulate Maya behavior exactly.

---

## What the sample contains

```text
FX_Physics_Showcase_Root
  MODEL_HeroMech
    HeroMech_Torso / Head / Arms / Legs
    HeroMech_BackpackEmitterMount

  ENV_Arena
    Arena_Ground
    Arena_Ramp

  FX_Particles
    FX_Sparks_ParticleSystem
    FX_SparksShape
    FX_Sparks_Emitter
    FX_Sparks_Gravity
    FX_Sparks_Turbulence

  PHYS_RigidBodies
    Phys_DropCrate
    Phys_BounceSphere
    PHYS_LegacyRigidSolver
    rigidBody metadata nodes

  PHYS_NCloth
    Cloth_Flag
    Cloth_FlagPole
    PHYS_Nucleus_Main
    PHYS_Flag_NCloth
    PHYS_FlagPole_NRigid
    PHYS_Flag_PinConstraint

  CAM_Lights
    CAM_DemoCamera
    LIGHT_Key
    LIGHT_FX_Orange
```

---

## Expected MAYAtoUnity behavior

| Area | Expected behavior |
|---|---|
| Transform hierarchy | Reconstructed / preserved as Unity GameObjects. |
| Mesh proxy nodes | Preserved as mesh node records; topology reconstruction is not the target of this `.ma` sample. |
| Materials / shadingEngine | Preserved through nodes and connections. |
| Camera / lights | Reconstructed or preserved depending on current parser support. |
| Particle nodes | Preserved and reported as FX source data. |
| Gravity / turbulence fields | Preserved and reported as simulation metadata. |
| rigidBody / rigidSolver | Preserved and reported as physics metadata. |
| nucleus / nCloth / nRigid / dynamicConstraint | Preserved and reported as nDynamics metadata. |
| setKeyframe commands | Preserved / reported as animation evidence. |
| Unsupported simulation behavior | Explicitly listed in import logs / reports instead of being silently lost. |

---

## What this sample should not claim

Do not claim that this sample proves full Maya-to-Unity simulation parity.

Avoid:

```text
Unity perfectly reproduces Maya particles.
Unity perfectly reproduces Maya rigid bodies.
Unity perfectly reproduces nCloth simulation.
```

Use:

```text
MAYAtoUnity preserves particle / physics / nDynamics source nodes, attributes, connections, and animation evidence for review.
Unsupported simulation behavior is reported instead of silently discarded.
```

---

## Optional visual source generation

The committed `.ma` sample is lightweight and review-oriented. To create a more visual Maya-authored source scene, run this script inside Autodesk Maya:

```python
import sys
sys.path.append(r"<repo>/Tools/MayaSamples")
import create_fx_physics_showcase_scene as demo

demo.build_scene(r"<repo>/Samples/FxPhysicsShowcase_Generated.ma")
```

Script:

```text
Tools/MayaSamples/create_fx_physics_showcase_scene.py
```

The generated file can be opened in Maya for visual review, then exported through the normal MAYAtoUnity workflow.

---

## Recommended validation steps

```text
1. Open the Unity project.
2. Select Samples/FxPhysicsShowcase.ma.
3. Run the MAYAtoUnity selected-file validation path.
4. Inspect the generated hierarchy.
5. Inspect node records and connection records.
6. Confirm particle / physics / nCloth nodes appear in the report.
7. Confirm unsupported simulation behavior is reported honestly.
```
