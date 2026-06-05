//Maya ASCII validation sample for MAYAtoUnity
//Purpose: constraint node and connection preservation validation.
requires maya "2024";
currentUnit -linear "cm" -angle "deg" -time "film";

createNode transform -n "ConstraintTarget";
    setAttr ".t" -type "double3" 2 0 0;

createNode transform -n "ConstraintDriven";
    setAttr ".t" -type "double3" 0 0 0;

createNode parentConstraint -n "ConstraintDriven_parentConstraint1" -p "ConstraintDriven";
    setAttr ".w0" 1;

connectAttr "ConstraintTarget.worldMatrix[0]" "ConstraintDriven_parentConstraint1.target[0].targetParentMatrix";
connectAttr "ConstraintTarget.rotatePivotTranslate" "ConstraintDriven_parentConstraint1.target[0].targetRotateTranslate";
connectAttr "ConstraintTarget.rotatePivot" "ConstraintDriven_parentConstraint1.target[0].targetRotatePivot";
connectAttr "ConstraintTarget.translate" "ConstraintDriven_parentConstraint1.target[0].targetTranslate";
connectAttr "ConstraintTarget.rotate" "ConstraintDriven_parentConstraint1.target[0].targetRotate";
connectAttr "ConstraintDriven_parentConstraint1.constraintTranslate" "ConstraintDriven.translate";
connectAttr "ConstraintDriven_parentConstraint1.constraintRotate" "ConstraintDriven.rotate";
