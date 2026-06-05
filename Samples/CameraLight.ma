//Maya ASCII validation sample for MAYAtoUnity
//Purpose: camera and light node preservation / reconstruction validation.
requires maya "2024";
currentUnit -linear "cm" -angle "deg" -time "film";

createNode transform -n "CameraRig";
    setAttr ".t" -type "double3" 0 3 8;
    setAttr ".r" -type "double3" -20 0 0;

createNode camera -n "MainCameraShape" -p "CameraRig";
    setAttr ".fl" 35;
    setAttr ".ncp" 0.1;
    setAttr ".fcp" 1000;

createNode transform -n "KeyLight";
    setAttr ".t" -type "double3" 3 5 4;
    setAttr ".r" -type "double3" -45 35 0;

createNode directionalLight -n "KeyLightShape" -p "KeyLight";
    setAttr ".cl" -type "float3" 1 0.95 0.85;
    setAttr ".in" 1.5;

createNode transform -n "FillLight";
    setAttr ".t" -type "double3" -4 2 2;

createNode pointLight -n "FillLightShape" -p "FillLight";
    setAttr ".cl" -type "float3" 0.4 0.55 1;
    setAttr ".in" 0.75;
