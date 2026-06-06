//Maya ASCII validation sample for MAYAtoUnity
//Purpose: demo scene with 3D model proxies, particles, rigid-body physics, nCloth-style physics nodes, animation keys, materials, camera, and light.
//Review note: this sample is intentionally designed for the Unity-only .ma preservation path.
//MAYAtoUnity should preserve node identities, hierarchy, attributes, connections, setKeyframe commands, and unsupported FX/physics nodes in reports.
//It does not claim full Maya particle / nDynamics / rigid-body simulation parity inside Unity.
requires maya "2024";
currentUnit -linear "cm" -angle "deg" -time "film";
fileInfo "application" "maya";
fileInfo "MAYAtoUnitySample" "FxPhysicsShowcase";
fileInfo "ValidationIntent" "3D model hierarchy + particle effect + physics node preservation";

// -----------------------------------------------------------------------------
// Scene root / organization
// -----------------------------------------------------------------------------
createNode transform -n "FX_Physics_Showcase_Root";
    setAttr ".t" -type "double3" 0 0 0;
    setAttr ".r" -type "double3" 0 0 0;
    setAttr ".s" -type "double3" 1 1 1;

createNode transform -n "MODEL_HeroMech" -p "FX_Physics_Showcase_Root";
    setAttr ".t" -type "double3" 0 0 0;
createNode transform -n "ENV_Arena" -p "FX_Physics_Showcase_Root";
createNode transform -n "FX_Particles" -p "FX_Physics_Showcase_Root";
createNode transform -n "PHYS_RigidBodies" -p "FX_Physics_Showcase_Root";
createNode transform -n "PHYS_NCloth" -p "FX_Physics_Showcase_Root";
createNode transform -n "CAM_Lights" -p "FX_Physics_Showcase_Root";

// -----------------------------------------------------------------------------
// 3D model proxy: hero mech built from mesh nodes and transforms
// -----------------------------------------------------------------------------
createNode transform -n "HeroMech_Torso" -p "MODEL_HeroMech";
    setAttr ".t" -type "double3" 0 2.5 0;
    setAttr ".s" -type "double3" 1.6 2.2 0.8;
createNode mesh -n "HeroMech_TorsoShape" -p "HeroMech_Torso";

createNode transform -n "HeroMech_Head" -p "MODEL_HeroMech";
    setAttr ".t" -type "double3" 0 4.25 0;
    setAttr ".s" -type "double3" 0.9 0.7 0.7;
createNode mesh -n "HeroMech_HeadShape" -p "HeroMech_Head";

createNode transform -n "HeroMech_LeftArm" -p "MODEL_HeroMech";
    setAttr ".t" -type "double3" -1.45 2.7 0;
    setAttr ".r" -type "double3" 0 0 -12;
    setAttr ".s" -type "double3" 0.35 1.6 0.35;
createNode mesh -n "HeroMech_LeftArmShape" -p "HeroMech_LeftArm";

createNode transform -n "HeroMech_RightArm" -p "MODEL_HeroMech";
    setAttr ".t" -type "double3" 1.45 2.7 0;
    setAttr ".r" -type "double3" 0 0 12;
    setAttr ".s" -type "double3" 0.35 1.6 0.35;
createNode mesh -n "HeroMech_RightArmShape" -p "HeroMech_RightArm";

createNode transform -n "HeroMech_LeftLeg" -p "MODEL_HeroMech";
    setAttr ".t" -type "double3" -0.55 0.8 0;
    setAttr ".s" -type "double3" 0.45 1.5 0.45;
createNode mesh -n "HeroMech_LeftLegShape" -p "HeroMech_LeftLeg";

createNode transform -n "HeroMech_RightLeg" -p "MODEL_HeroMech";
    setAttr ".t" -type "double3" 0.55 0.8 0;
    setAttr ".s" -type "double3" 0.45 1.5 0.45;
createNode mesh -n "HeroMech_RightLegShape" -p "HeroMech_RightLeg";

createNode transform -n "HeroMech_BackpackEmitterMount" -p "MODEL_HeroMech";
    setAttr ".t" -type "double3" 0 3.1 -0.85;
createNode locator -n "HeroMech_BackpackEmitterMountShape" -p "HeroMech_BackpackEmitterMount";

// Simple arena / colliders
createNode transform -n "Arena_Ground" -p "ENV_Arena";
    setAttr ".t" -type "double3" 0 -0.05 0;
    setAttr ".s" -type "double3" 8 0.1 8;
createNode mesh -n "Arena_GroundShape" -p "Arena_Ground";

createNode transform -n "Arena_Ramp" -p "ENV_Arena";
    setAttr ".t" -type "double3" -2.75 0.35 1.25;
    setAttr ".r" -type "double3" 0 0 -18;
    setAttr ".s" -type "double3" 2.5 0.25 1.5;
createNode mesh -n "Arena_RampShape" -p "Arena_Ramp";

createNode transform -n "Phys_DropCrate" -p "PHYS_RigidBodies";
    setAttr ".t" -type "double3" 2.75 5.5 0;
    setAttr ".r" -type "double3" 12 18 0;
    setAttr ".s" -type "double3" 0.8 0.8 0.8;
createNode mesh -n "Phys_DropCrateShape" -p "Phys_DropCrate";

createNode transform -n "Phys_BounceSphere" -p "PHYS_RigidBodies";
    setAttr ".t" -type "double3" 1.25 3.4 -1.5;
    setAttr ".s" -type "double3" 0.65 0.65 0.65;
createNode mesh -n "Phys_BounceSphereShape" -p "Phys_BounceSphere";

// -----------------------------------------------------------------------------
// Materials / shading connections
// -----------------------------------------------------------------------------
createNode lambert -n "MAT_HeroMech_Blue";
    setAttr ".c" -type "float3" 0.15 0.35 0.9;
    setAttr ".dc" 0.85;
createNode lambert -n "MAT_HeroMech_OrangeGlow";
    setAttr ".c" -type "float3" 1.0 0.45 0.08;
    setAttr ".incandescence" -type "float3" 0.7 0.22 0.03;
createNode lambert -n "MAT_Arena_Grey";
    setAttr ".c" -type "float3" 0.35 0.35 0.38;
createNode lambert -n "MAT_Physics_Crate";
    setAttr ".c" -type "float3" 0.75 0.42 0.16;
createNode lambert -n "MAT_Cloth_Red";
    setAttr ".c" -type "float3" 0.85 0.08 0.08;

createNode shadingEngine -n "SG_HeroMech_Blue";
createNode shadingEngine -n "SG_HeroMech_OrangeGlow";
createNode shadingEngine -n "SG_Arena_Grey";
createNode shadingEngine -n "SG_Physics_Crate";
createNode shadingEngine -n "SG_Cloth_Red";

connectAttr "MAT_HeroMech_Blue.outColor" "SG_HeroMech_Blue.surfaceShader";
connectAttr "MAT_HeroMech_OrangeGlow.outColor" "SG_HeroMech_OrangeGlow.surfaceShader";
connectAttr "MAT_Arena_Grey.outColor" "SG_Arena_Grey.surfaceShader";
connectAttr "MAT_Physics_Crate.outColor" "SG_Physics_Crate.surfaceShader";
connectAttr "MAT_Cloth_Red.outColor" "SG_Cloth_Red.surfaceShader";

connectAttr "HeroMech_TorsoShape.instObjGroups[0]" "SG_HeroMech_Blue.dagSetMembers[0]";
connectAttr "HeroMech_HeadShape.instObjGroups[0]" "SG_HeroMech_Blue.dagSetMembers[1]";
connectAttr "HeroMech_LeftArmShape.instObjGroups[0]" "SG_HeroMech_Blue.dagSetMembers[2]";
connectAttr "HeroMech_RightArmShape.instObjGroups[0]" "SG_HeroMech_Blue.dagSetMembers[3]";
connectAttr "HeroMech_LeftLegShape.instObjGroups[0]" "SG_HeroMech_Blue.dagSetMembers[4]";
connectAttr "HeroMech_RightLegShape.instObjGroups[0]" "SG_HeroMech_Blue.dagSetMembers[5]";
connectAttr "Arena_GroundShape.instObjGroups[0]" "SG_Arena_Grey.dagSetMembers[0]";
connectAttr "Arena_RampShape.instObjGroups[0]" "SG_Arena_Grey.dagSetMembers[1]";
connectAttr "Phys_DropCrateShape.instObjGroups[0]" "SG_Physics_Crate.dagSetMembers[0]";
connectAttr "Phys_BounceSphereShape.instObjGroups[0]" "SG_HeroMech_OrangeGlow.dagSetMembers[0]";

// -----------------------------------------------------------------------------
// Particle FX: sparks emitted from mech backpack, affected by gravity and turbulence
// -----------------------------------------------------------------------------
createNode transform -n "FX_Sparks_ParticleSystem" -p "FX_Particles";
    setAttr ".t" -type "double3" 0 3.1 -0.85;
createNode particle -n "FX_SparksShape" -p "FX_Sparks_ParticleSystem";
    setAttr ".maxCount" 1000;
    setAttr ".lifespanMode" 1;
    setAttr ".lifespan" 1.25;
    setAttr ".particleRenderType" 8;
    addAttr -ln "MAYAtoUnityRole" -dt "string";
    setAttr ".MAYAtoUnityRole" -type "string" "spark particle effect source; preserve/report unsupported simulation";

createNode pointEmitter -n "FX_Sparks_Emitter";
    setAttr ".rate" 180;
    setAttr ".speed" 5.5;
    setAttr ".spread" 0.55;
    setAttr ".directionX" 0;
    setAttr ".directionY" 1;
    setAttr ".directionZ" -0.35;

createNode gravityField -n "FX_Sparks_Gravity";
    setAttr ".magnitude" 9.8;
    setAttr ".directionX" 0;
    setAttr ".directionY" -1;
    setAttr ".directionZ" 0;

createNode turbulenceField -n "FX_Sparks_Turbulence";
    setAttr ".magnitude" 8.0;
    setAttr ".attenuation" 0.2;
    setAttr ".frequency" 0.65;
    setAttr ".phaseX" 0.15;
    setAttr ".phaseY" 0.35;
    setAttr ".phaseZ" 0.55;

connectAttr "HeroMech_BackpackEmitterMount.worldMatrix[0]" "FX_Sparks_Emitter.ownerMatrix";
connectAttr "FX_Sparks_Emitter.outParticle" "FX_SparksShape.inParticle";
connectAttr "FX_Sparks_Gravity.message" "FX_SparksShape.fieldData[0]";
connectAttr "FX_Sparks_Turbulence.message" "FX_SparksShape.fieldData[1]";

setKeyframe -t 1 -at "rate" -v 0 "FX_Sparks_Emitter";
setKeyframe -t 12 -at "rate" -v 180 "FX_Sparks_Emitter";
setKeyframe -t 72 -at "rate" -v 260 "FX_Sparks_Emitter";
setKeyframe -t 120 -at "rate" -v 0 "FX_Sparks_Emitter";

// -----------------------------------------------------------------------------
// Physics: legacy rigid-body style metadata and nDynamics-style node preservation
// -----------------------------------------------------------------------------
createNode rigidSolver -n "PHYS_LegacyRigidSolver";
    setAttr ".gravity" -type "double3" 0 -9.8 0;
    setAttr ".currentTime" 1;

createNode rigidBody -n "PHYS_Ground_PassiveRigidBody";
    setAttr ".active" 0;
    setAttr ".mass" 0;
    setAttr ".bounciness" 0.35;
    setAttr ".damping" 0.2;

createNode rigidBody -n "PHYS_Ramp_PassiveRigidBody";
    setAttr ".active" 0;
    setAttr ".mass" 0;
    setAttr ".bounciness" 0.25;
    setAttr ".damping" 0.15;

createNode rigidBody -n "PHYS_DropCrate_ActiveRigidBody";
    setAttr ".active" 1;
    setAttr ".mass" 8.0;
    setAttr ".bounciness" 0.45;
    setAttr ".damping" 0.08;

createNode rigidBody -n "PHYS_BounceSphere_ActiveRigidBody";
    setAttr ".active" 1;
    setAttr ".mass" 2.0;
    setAttr ".bounciness" 0.85;
    setAttr ".damping" 0.02;

connectAttr "Arena_Ground.message" "PHYS_Ground_PassiveRigidBody.inputGeometryMsg";
connectAttr "Arena_Ramp.message" "PHYS_Ramp_PassiveRigidBody.inputGeometryMsg";
connectAttr "Phys_DropCrate.message" "PHYS_DropCrate_ActiveRigidBody.inputGeometryMsg";
connectAttr "Phys_BounceSphere.message" "PHYS_BounceSphere_ActiveRigidBody.inputGeometryMsg";
connectAttr "PHYS_LegacyRigidSolver.message" "PHYS_DropCrate_ActiveRigidBody.solver";
connectAttr "PHYS_LegacyRigidSolver.message" "PHYS_BounceSphere_ActiveRigidBody.solver";

setKeyframe -t 1 -at "translateY" -v 5.5 "Phys_DropCrate";
setKeyframe -t 48 -at "translateY" -v 1.4 "Phys_DropCrate";
setKeyframe -t 72 -at "rotateZ" -v 45 "Phys_DropCrate";
setKeyframe -t 120 -at "translateY" -v 0.85 "Phys_DropCrate";

// nCloth-style flag: mesh proxy + nucleus / nCloth / nRigid / dynamicConstraint nodes
createNode transform -n "Cloth_Flag" -p "PHYS_NCloth";
    setAttr ".t" -type "double3" -3.5 3.2 0;
    setAttr ".r" -type "double3" 0 0 0;
    setAttr ".s" -type "double3" 1.2 0.8 1.0;
createNode mesh -n "Cloth_FlagShape" -p "Cloth_Flag";

createNode transform -n "Cloth_FlagPole" -p "PHYS_NCloth";
    setAttr ".t" -type "double3" -4.15 1.9 0;
    setAttr ".s" -type "double3" 0.08 3.8 0.08;
createNode mesh -n "Cloth_FlagPoleShape" -p "Cloth_FlagPole";

createNode nucleus -n "PHYS_Nucleus_Main";
    setAttr ".startFrame" 1;
    setAttr ".gravity" 9.8;
    setAttr ".windSpeed" 1.5;
    setAttr ".windDirection" -type "double3" 1 0 0.25;

createNode nCloth -n "PHYS_Flag_NCloth";
    setAttr ".thickness" 0.02;
    setAttr ".stretchResistance" 35;
    setAttr ".bendResistance" 0.25;
    addAttr -ln "MAYAtoUnityRole" -dt "string";
    setAttr ".MAYAtoUnityRole" -type "string" "nCloth flag simulation source; preserve/report unsupported simulation";

createNode nRigid -n "PHYS_FlagPole_NRigid";
    setAttr ".thickness" 0.05;
    setAttr ".bounce" 0.1;

createNode dynamicConstraint -n "PHYS_Flag_PinConstraint";
    setAttr ".constraintMethod" 0;
    setAttr ".strength" 1.0;

connectAttr "Cloth_FlagShape.worldMesh[0]" "PHYS_Flag_NCloth.inputMesh";
connectAttr "PHYS_Flag_NCloth.outputMesh" "Cloth_FlagShape.inMesh";
connectAttr "Cloth_FlagPoleShape.worldMesh[0]" "PHYS_FlagPole_NRigid.inputMesh";
connectAttr "PHYS_Nucleus_Main.outputObjects[0]" "PHYS_Flag_NCloth.nextState";
connectAttr "PHYS_Nucleus_Main.outputObjects[1]" "PHYS_FlagPole_NRigid.nextState";
connectAttr "PHYS_Flag_PinConstraint.constraintData" "PHYS_Flag_NCloth.inputForce[0]";
connectAttr "Cloth_FlagShape.instObjGroups[0]" "SG_Cloth_Red.dagSetMembers[0]";

// -----------------------------------------------------------------------------
// Camera / light setup
// -----------------------------------------------------------------------------
createNode transform -n "CAM_DemoCamera" -p "CAM_Lights";
    setAttr ".t" -type "double3" 6 4.5 8;
    setAttr ".r" -type "double3" -28 38 0;
createNode camera -n "CAM_DemoCameraShape" -p "CAM_DemoCamera";
    setAttr ".fl" 35;
    setAttr ".ncp" 0.1;
    setAttr ".fcp" 1000;

createNode transform -n "LIGHT_Key" -p "CAM_Lights";
    setAttr ".t" -type "double3" 3 6 4;
    setAttr ".r" -type "double3" -45 35 0;
createNode directionalLight -n "LIGHT_KeyShape" -p "LIGHT_Key";
    setAttr ".intensity" 1.25;
    setAttr ".color" -type "float3" 1.0 0.92 0.82;

createNode transform -n "LIGHT_FX_Orange" -p "CAM_Lights";
    setAttr ".t" -type "double3" 0 3.5 -2;
createNode pointLight -n "LIGHT_FX_OrangeShape" -p "LIGHT_FX_Orange";
    setAttr ".intensity" 4.0;
    setAttr ".color" -type "float3" 1.0 0.35 0.08;

// -----------------------------------------------------------------------------
// Importer-facing notes as custom nodes / attributes
// -----------------------------------------------------------------------------
createNode network -n "MAYAtoUnity_DemoNotes";
    addAttr -ln "ExpectedBehavior" -dt "string";
    setAttr ".ExpectedBehavior" -type "string" "Transforms, materials, camera, lights, connections, keyframes, particle nodes, rigidBody nodes, nucleus/nCloth/nRigid/dynamicConstraint nodes should be preserved and reported. Unity simulation parity is not expected.";
    addAttr -ln "RecommendedReview" -dt "string";
    setAttr ".RecommendedReview" -type "string" "Run Tools/MAYAtoUnity/Validate Selected .ma, then inspect hierarchy, node records, connection records, unsupported feature report, and provenance.";
