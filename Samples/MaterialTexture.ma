//Maya ASCII validation sample for MAYAtoUnity
//Purpose: material, file texture, shadingEngine, and connection preservation.
requires maya "2024";
currentUnit -linear "cm" -angle "deg" -time "film";

createNode transform -n "MaterialSampleRoot";
createNode mesh -n "MaterialSampleShape" -p "MaterialSampleRoot";

createNode lambert -n "MAT_Sample_Lambert";
    setAttr ".c" -type "float3" 0.8 0.25 0.1;
    setAttr ".dc" 0.85;

createNode file -n "TEX_Sample_Diffuse";
    setAttr ".ftn" -type "string" "Textures/sample_diffuse.png";

createNode place2dTexture -n "TEX_Sample_Placer";
createNode shadingEngine -n "SG_Sample";

connectAttr "TEX_Sample_Placer.outUV" "TEX_Sample_Diffuse.uv";
connectAttr "TEX_Sample_Diffuse.outColor" "MAT_Sample_Lambert.color";
connectAttr "MAT_Sample_Lambert.outColor" "SG_Sample.surfaceShader";
connectAttr "MaterialSampleShape.instObjGroups[0]" "SG_Sample.dagSetMembers[0]";
