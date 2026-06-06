"""
Create a MAYAtoUnity FX / physics showcase scene.

Run inside Autodesk Maya with Python, then save the generated scene as Maya ASCII:

    import sys
    sys.path.append(r"<repo>/Tools/MayaSamples")
    import create_fx_physics_showcase_scene as demo
    demo.build_scene(r"<repo>/Samples/FxPhysicsShowcase_Generated.ma")

The generated scene is intended as a visual authoring source for the MAYAtoUnity
portfolio demo. The committed Samples/FxPhysicsShowcase.ma is a lightweight
validation/audit sample for the Unity-only .ma preservation path.
"""

from __future__ import annotations

import os

import maya.cmds as cmds
import maya.mel as mel


def _mat(name: str, color):
    shader = cmds.shadingNode("lambert", asShader=True, name=name)
    cmds.setAttr(shader + ".color", color[0], color[1], color[2], type="double3")
    sg = cmds.sets(renderable=True, noSurfaceShader=True, empty=True, name=name.replace("MAT_", "SG_"))
    cmds.connectAttr(shader + ".outColor", sg + ".surfaceShader", force=True)
    return shader, sg


def _assign(obj: str, sg: str):
    try:
        cmds.sets(obj, edit=True, forceElement=sg)
    except Exception:
        pass


def _cube(name: str, parent: str, t, s, sg: str):
    obj = cmds.polyCube(name=name, width=1, height=1, depth=1)[0]
    cmds.parent(obj, parent)
    cmds.xform(obj, translation=t, scale=s)
    _assign(obj, sg)
    return obj


def _sphere(name: str, parent: str, t, radius: float, sg: str):
    obj = cmds.polySphere(name=name, radius=radius, subdivisionsX=24, subdivisionsY=16)[0]
    cmds.parent(obj, parent)
    cmds.xform(obj, translation=t)
    _assign(obj, sg)
    return obj


def _safe_rigid_body(obj: str, passive: bool, name: str, **kwargs):
    """Create a legacy rigidBody if the command is available in the Maya install."""
    try:
        return cmds.rigidBody(obj, passive=passive, name=name, **kwargs)
    except Exception as exc:
        cmds.warning("Could not create rigidBody for {0}: {1}".format(obj, exc))
        return None


def _safe_ncloth(flag_mesh: str, pole_mesh: str):
    """Create a simple nCloth setup if Maya nDynamics commands are available."""
    try:
        cmds.select(flag_mesh, replace=True)
        mel.eval("createNCloth 0;")
        cmds.select(pole_mesh, replace=True)
        mel.eval("makeCollideNCloth;")
    except Exception as exc:
        cmds.warning("Could not create nCloth setup: {0}".format(exc))


def build_scene(output_path: str | None = None):
    cmds.file(new=True, force=True)
    cmds.currentUnit(linear="cm", angle="deg", time="film")
    cmds.playbackOptions(minTime=1, maxTime=120, animationStartTime=1, animationEndTime=120)

    root = cmds.group(empty=True, name="FX_Physics_Showcase_Root")
    model_grp = cmds.group(empty=True, name="MODEL_HeroMech", parent=root)
    env_grp = cmds.group(empty=True, name="ENV_Arena", parent=root)
    fx_grp = cmds.group(empty=True, name="FX_Particles", parent=root)
    phys_grp = cmds.group(empty=True, name="PHYS_RigidBodies", parent=root)
    cloth_grp = cmds.group(empty=True, name="PHYS_NCloth", parent=root)
    cam_grp = cmds.group(empty=True, name="CAM_Lights", parent=root)

    _, sg_blue = _mat("MAT_HeroMech_Blue", (0.15, 0.35, 0.9))
    _, sg_orange = _mat("MAT_HeroMech_OrangeGlow", (1.0, 0.45, 0.08))
    _, sg_grey = _mat("MAT_Arena_Grey", (0.35, 0.35, 0.38))
    _, sg_crate = _mat("MAT_Physics_Crate", (0.75, 0.42, 0.16))
    _, sg_cloth = _mat("MAT_Cloth_Red", (0.85, 0.08, 0.08))

    # Hero mech model.
    _cube("HeroMech_Torso", model_grp, (0, 2.5, 0), (1.6, 2.2, 0.8), sg_blue)
    _cube("HeroMech_Head", model_grp, (0, 4.25, 0), (0.9, 0.7, 0.7), sg_blue)
    left_arm = _cube("HeroMech_LeftArm", model_grp, (-1.45, 2.7, 0), (0.35, 1.6, 0.35), sg_blue)
    right_arm = _cube("HeroMech_RightArm", model_grp, (1.45, 2.7, 0), (0.35, 1.6, 0.35), sg_blue)
    cmds.rotate(0, 0, -12, left_arm)
    cmds.rotate(0, 0, 12, right_arm)
    _cube("HeroMech_LeftLeg", model_grp, (-0.55, 0.8, 0), (0.45, 1.5, 0.45), sg_blue)
    _cube("HeroMech_RightLeg", model_grp, (0.55, 0.8, 0), (0.45, 1.5, 0.45), sg_blue)
    emitter_mount = cmds.spaceLocator(name="HeroMech_BackpackEmitterMount")[0]
    cmds.parent(emitter_mount, model_grp)
    cmds.xform(emitter_mount, translation=(0, 3.1, -0.85))

    # Arena and physics props.
    ground = _cube("Arena_Ground", env_grp, (0, -0.05, 0), (8, 0.1, 8), sg_grey)
    ramp = _cube("Arena_Ramp", env_grp, (-2.75, 0.35, 1.25), (2.5, 0.25, 1.5), sg_grey)
    cmds.rotate(0, 0, -18, ramp)
    crate = _cube("Phys_DropCrate", phys_grp, (2.75, 5.5, 0), (0.8, 0.8, 0.8), sg_crate)
    cmds.rotate(12, 18, 0, crate)
    ball = _sphere("Phys_BounceSphere", phys_grp, (1.25, 3.4, -1.5), 0.65, sg_orange)

    _safe_rigid_body(ground, passive=True, name="PHYS_Ground_PassiveRigidBody", bounciness=0.35, damping=0.2)
    _safe_rigid_body(ramp, passive=True, name="PHYS_Ramp_PassiveRigidBody", bounciness=0.25, damping=0.15)
    _safe_rigid_body(crate, passive=False, name="PHYS_DropCrate_ActiveRigidBody", mass=8.0, bounciness=0.45, damping=0.08)
    _safe_rigid_body(ball, passive=False, name="PHYS_BounceSphere_ActiveRigidBody", mass=2.0, bounciness=0.85, damping=0.02)

    # Animated fallback for importers that preserve keyframes but do not evaluate rigid bodies.
    cmds.setKeyframe(crate, time=1, attribute="translateY", value=5.5)
    cmds.setKeyframe(crate, time=48, attribute="translateY", value=1.4)
    cmds.setKeyframe(crate, time=72, attribute="rotateZ", value=45)
    cmds.setKeyframe(crate, time=120, attribute="translateY", value=0.85)

    # Particle sparks.
    particle = cmds.particle(name="FX_Sparks_Particles")[0]
    cmds.parent(particle, fx_grp)
    emitter = cmds.emitter(name="FX_Sparks_Emitter", type="omni", rate=180, speed=5.5, spread=0.55)[0]
    cmds.xform(emitter, translation=(0, 3.1, -0.85))
    cmds.connectDynamic(particle, em=emitter)
    grav = cmds.gravity(name="FX_Sparks_Gravity", magnitude=9.8, directionY=-1)[0]
    turb = cmds.turbulence(name="FX_Sparks_Turbulence", magnitude=8.0, attenuation=0.2, frequency=0.65)[0]
    cmds.connectDynamic(particle, f=grav)
    cmds.connectDynamic(particle, f=turb)
    cmds.setAttr(particle + ".lifespanMode", 1)
    cmds.setAttr(particle + ".lifespan", 1.25)
    cmds.setKeyframe(emitter, time=1, attribute="rate", value=0)
    cmds.setKeyframe(emitter, time=12, attribute="rate", value=180)
    cmds.setKeyframe(emitter, time=72, attribute="rate", value=260)
    cmds.setKeyframe(emitter, time=120, attribute="rate", value=0)

    # Flag cloth source mesh.
    flag = cmds.polyPlane(name="Cloth_Flag", width=2.4, height=1.2, subdivisionsX=12, subdivisionsY=6)[0]
    cmds.parent(flag, cloth_grp)
    cmds.xform(flag, translation=(-3.5, 3.2, 0), rotation=(0, 0, 0))
    _assign(flag, sg_cloth)
    pole = _cube("Cloth_FlagPole", cloth_grp, (-4.15, 1.9, 0), (0.08, 3.8, 0.08), sg_grey)
    _safe_ncloth(flag, pole)

    # Camera and lights.
    cam, cam_shape = cmds.camera(name="CAM_DemoCamera")
    cmds.parent(cam, cam_grp)
    cmds.xform(cam, translation=(6, 4.5, 8), rotation=(-28, 38, 0))
    cmds.setAttr(cam_shape + ".focalLength", 35)
    cmds.lookThru(cam)

    key = cmds.directionalLight(name="LIGHT_KeyShape", intensity=1.25, rgb=(1.0, 0.92, 0.82))
    key_transform = cmds.listRelatives(key, parent=True)[0]
    key_transform = cmds.rename(key_transform, "LIGHT_Key")
    cmds.parent(key_transform, cam_grp)
    cmds.xform(key_transform, translation=(3, 6, 4), rotation=(-45, 35, 0))

    fx_light = cmds.pointLight(name="LIGHT_FX_OrangeShape", intensity=4.0, rgb=(1.0, 0.35, 0.08))
    fx_light_transform = cmds.listRelatives(fx_light, parent=True)[0]
    fx_light_transform = cmds.rename(fx_light_transform, "LIGHT_FX_Orange")
    cmds.parent(fx_light_transform, cam_grp)
    cmds.xform(fx_light_transform, translation=(0, 3.5, -2))

    # Importer-facing metadata.
    notes = cmds.createNode("network", name="MAYAtoUnity_DemoNotes")
    cmds.addAttr(notes, longName="ExpectedBehavior", dataType="string")
    cmds.setAttr(
        notes + ".ExpectedBehavior",
        "Transforms, materials, camera, lights, connections, keyframes, particles, rigid bodies, and nCloth nodes should be preserved/reported. Unity simulation parity is not expected.",
        type="string",
    )

    if output_path:
        out = os.path.abspath(output_path)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        cmds.file(rename=out)
        cmds.file(save=True, type="mayaAscii")
        print("Saved MAYAtoUnity FX / physics showcase scene:", out)

    return root
