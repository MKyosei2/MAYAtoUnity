//Maya ASCII validation sample for MAYAtoUnity
//Purpose: setKeyframe / animCurve command preservation and report validation.
requires maya "2024";
currentUnit -linear "cm" -angle "deg" -time "film";

createNode transform -n "AnimatedCubeProxy";
    setAttr ".t" -type "double3" 0 0 0;
    setAttr ".r" -type "double3" 0 0 0;
    setAttr ".s" -type "double3" 1 1 1;

createNode animCurveTL -n "AnimatedCubeProxy_translateX";
createNode animCurveTA -n "AnimatedCubeProxy_rotateY";

setKeyframe -t 1 -at "translateX" -v 0 "AnimatedCubeProxy";
setKeyframe -t 24 -at "translateX" -v 5 "AnimatedCubeProxy";
setKeyframe -t 48 -at "translateX" -v 0 "AnimatedCubeProxy";

setKeyframe -t 1 -at "rotateY" -v 0 "AnimatedCubeProxy";
setKeyframe -t 24 -at "rotateY" -v 180 "AnimatedCubeProxy";
setKeyframe -t 48 -at "rotateY" -v 360 "AnimatedCubeProxy";

connectAttr "AnimatedCubeProxy_translateX.output" "AnimatedCubeProxy.translateX";
connectAttr "AnimatedCubeProxy_rotateY.output" "AnimatedCubeProxy.rotateY";
