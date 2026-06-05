//Maya ASCII validation sample for MAYAtoUnity
//Purpose: transform hierarchy, parent links, setAttr parsing, connection-free import.
requires maya "2024";
currentUnit -linear "cm" -angle "deg" -time "film";
fileInfo "application" "maya";

createNode transform -n "Root";
    setAttr ".t" -type "double3" 0 0 0;
    setAttr ".r" -type "double3" 0 0 0;
    setAttr ".s" -type "double3" 1 1 1;

createNode transform -n "Child_A" -p "Root";
    setAttr ".t" -type "double3" 1 2 3;
    setAttr ".r" -type "double3" 0 45 0;
    setAttr ".s" -type "double3" 1 1 1;

createNode transform -n "Child_B" -p "Root";
    setAttr ".t" -type "double3" -1 0 2;
    setAttr ".r" -type "double3" 0 0 30;
    setAttr ".s" -type "double3" 0.5 0.5 0.5;

createNode transform -n "GrandChild" -p "Child_A";
    setAttr ".t" -type "double3" 0 1 0;
    setAttr ".s" -type "double3" 0.25 0.25 0.25;
