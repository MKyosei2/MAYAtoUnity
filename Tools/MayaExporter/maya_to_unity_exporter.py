# MAYAtoUnity Maya-side exporter prototype
# Run inside Autodesk Maya's Python environment.
#
# This exporter is the production-oriented bridge path for MAYAtoUnity:
# Maya scene -> deterministic JSON -> Unity importer.
#
# Usage in Maya Script Editor:
#   import sys
#   sys.path.append(r"path/to/Tools/MayaExporter")
#   import maya_to_unity_exporter
#   maya_to_unity_exporter.export_scene(r"C:/temp/maya_unity_scene.json")

from __future__ import annotations

import json
import os
import hashlib
from typing import Any, Dict, List, Optional

try:
    import maya.cmds as cmds
except Exception:  # Allows documentation tools to import the file outside Maya.
    cmds = None

SCHEMA_VERSION = 1


def _require_maya() -> None:
    if cmds is None:
        raise RuntimeError("This exporter must be run inside Autodesk Maya with maya.cmds available.")


def _safe_get_attr(plug: str, default: Any = None) -> Any:
    try:
        if cmds.objExists(plug):
            return cmds.getAttr(plug)
    except Exception:
        pass
    return default


def _node_uuid(node: str) -> str:
    try:
        values = cmds.ls(node, uuid=True) or []
        return values[0] if values else ""
    except Exception:
        return ""


def _full_path(node: str) -> str:
    try:
        values = cmds.ls(node, long=True) or []
        return values[0] if values else node
    except Exception:
        return node


def _parent_path(node: str) -> str:
    try:
        parents = cmds.listRelatives(node, parent=True, fullPath=True) or []
        return parents[0] if parents else ""
    except Exception:
        return ""


def _matrix_list(node: str) -> List[float]:
    try:
        m = cmds.xform(node, q=True, matrix=True, objectSpace=True)
        return [float(x) for x in m]
    except Exception:
        return []


def _world_matrix_list(node: str) -> List[float]:
    try:
        m = cmds.xform(node, q=True, matrix=True, worldSpace=True)
        return [float(x) for x in m]
    except Exception:
        return []


def _float3_attr(node: str, attr: str, default: Optional[List[float]] = None) -> List[float]:
    default = default or [0.0, 0.0, 0.0]
    try:
        value = cmds.getAttr(node + "." + attr)
        if isinstance(value, list) or isinstance(value, tuple):
            if len(value) > 0 and isinstance(value[0], (list, tuple)):
                return [float(value[0][0]), float(value[0][1]), float(value[0][2])]
            if len(value) >= 3:
                return [float(value[0]), float(value[1]), float(value[2])]
    except Exception:
        pass
    return default


def _collect_nodes() -> List[Dict[str, Any]]:
    output = []
    for node in sorted(cmds.ls() or []):
        try:
            node_type = cmds.nodeType(node)
        except Exception:
            node_type = "unknown"
        output.append({
            "name": node,
            "path": _full_path(node),
            "type": node_type,
            "uuid": _node_uuid(node),
            "parentPath": _parent_path(node),
        })
    return output


def _collect_transforms() -> List[Dict[str, Any]]:
    output = []
    for node in sorted(cmds.ls(type="transform", long=True) or []):
        short = node.split("|")[-1]
        output.append({
            "name": short,
            "path": node,
            "parentPath": _parent_path(node),
            "uuid": _node_uuid(node),
            "localPosition": _float3_attr(node, "translate", [0.0, 0.0, 0.0]),
            "localRotation": _float3_attr(node, "rotate", [0.0, 0.0, 0.0]),
            "localScale": _float3_attr(node, "scale", [1.0, 1.0, 1.0]),
            "localMatrix": _matrix_list(node),
            "worldMatrix": _world_matrix_list(node),
        })
    return output


def _collect_meshes() -> List[Dict[str, Any]]:
    meshes = []
    for shape in sorted(cmds.ls(type="mesh", long=True) or []):
        transform = _parent_path(shape)
        mesh_data: Dict[str, Any] = {
            "name": shape.split("|")[-1],
            "path": shape,
            "parentPath": transform,
            "uuid": _node_uuid(shape),
            "vertexCount": 0,
            "triangleCount": 0,
            "vertices": [],
            "normals": [],
            "uvs": [],
            "indices": [],
            "materials": [],
        }

        try:
            vertex_count = cmds.polyEvaluate(shape, vertex=True) or 0
            triangle_count = cmds.polyEvaluate(shape, triangle=True) or 0
            mesh_data["vertexCount"] = int(vertex_count)
            mesh_data["triangleCount"] = int(triangle_count)
        except Exception:
            pass

        # Exporting full topology robustly is best done with maya.api.OpenMaya.
        # This first prototype records counts, paths, material assignments, and source identity.
        # Full vertex/index export is the next milestone.
        try:
            shading_engines = cmds.listConnections(shape, type="shadingEngine") or []
            mesh_data["materials"] = sorted(set(shading_engines))
        except Exception:
            pass

        meshes.append(mesh_data)
    return meshes


def _collect_materials() -> List[Dict[str, Any]]:
    material_types = ["lambert", "phong", "blinn", "surfaceShader", "aiStandardSurface", "standardSurface"]
    mats = []
    seen = set()
    for t in material_types:
        for node in cmds.ls(type=t) or []:
            if node in seen:
                continue
            seen.add(node)
            mats.append({
                "name": node,
                "type": cmds.nodeType(node),
                "uuid": _node_uuid(node),
                "color": _safe_get_attr(node + ".color", None),
                "transparency": _safe_get_attr(node + ".transparency", None),
            })
    return sorted(mats, key=lambda x: x["name"])


def _collect_textures() -> List[Dict[str, Any]]:
    textures = []
    for node in sorted(cmds.ls(type="file") or []):
        textures.append({
            "name": node,
            "type": "file",
            "uuid": _node_uuid(node),
            "fileTextureName": _safe_get_attr(node + ".fileTextureName", "") or _safe_get_attr(node + ".ftn", ""),
            "colorSpace": _safe_get_attr(node + ".colorSpace", ""),
        })
    return textures


def _collect_cameras() -> List[Dict[str, Any]]:
    output = []
    for shape in sorted(cmds.ls(type="camera", long=True) or []):
        output.append({
            "name": shape.split("|")[-1],
            "path": shape,
            "parentPath": _parent_path(shape),
            "uuid": _node_uuid(shape),
            "focalLength": _safe_get_attr(shape + ".focalLength", 35.0),
            "nearClipPlane": _safe_get_attr(shape + ".nearClipPlane", 0.1),
            "farClipPlane": _safe_get_attr(shape + ".farClipPlane", 1000.0),
        })
    return output


def _collect_lights() -> List[Dict[str, Any]]:
    light_types = ["ambientLight", "directionalLight", "pointLight", "spotLight", "areaLight"]
    output = []
    for t in light_types:
        for shape in sorted(cmds.ls(type=t, long=True) or []):
            output.append({
                "name": shape.split("|")[-1],
                "path": shape,
                "parentPath": _parent_path(shape),
                "type": t,
                "uuid": _node_uuid(shape),
                "color": _safe_get_attr(shape + ".color", None),
                "intensity": _safe_get_attr(shape + ".intensity", 1.0),
            })
    return output


def _collect_joints() -> List[Dict[str, Any]]:
    output = []
    for node in sorted(cmds.ls(type="joint", long=True) or []):
        output.append({
            "name": node.split("|")[-1],
            "path": node,
            "parentPath": _parent_path(node),
            "uuid": _node_uuid(node),
            "localPosition": _float3_attr(node, "translate", [0.0, 0.0, 0.0]),
            "localRotation": _float3_attr(node, "rotate", [0.0, 0.0, 0.0]),
            "localScale": _float3_attr(node, "scale", [1.0, 1.0, 1.0]),
            "jointOrient": _float3_attr(node, "jointOrient", [0.0, 0.0, 0.0]),
            "worldMatrix": _world_matrix_list(node),
        })
    return output


def _collect_unsupported() -> List[Dict[str, Any]]:
    supported = {
        "transform", "joint", "mesh", "camera",
        "ambientLight", "directionalLight", "pointLight", "spotLight", "areaLight",
        "lambert", "phong", "blinn", "surfaceShader", "aiStandardSurface", "standardSurface",
        "file", "place2dTexture", "shadingEngine",
        "skinCluster", "blendShape",
    }
    unsupported = []
    for node in sorted(cmds.ls() or []):
        try:
            t = cmds.nodeType(node)
        except Exception:
            t = "unknown"
        if t not in supported:
            unsupported.append({"name": node, "type": t, "reason": "not in exporter v1 support set"})
    return unsupported


def _scene_file_hash() -> str:
    try:
        path = cmds.file(q=True, sceneName=True)
        if not path or not os.path.exists(path):
            return ""
        h = hashlib.sha256()
        with open(path, "rb") as f:
            for chunk in iter(lambda: f.read(1024 * 1024), b""):
                h.update(chunk)
        return h.hexdigest()
    except Exception:
        return ""


def build_scene_dict() -> Dict[str, Any]:
    _require_maya()
    source_path = cmds.file(q=True, sceneName=True) or ""
    return {
        "schemaVersion": SCHEMA_VERSION,
        "sourceMayaFile": source_path,
        "sourceHash": _scene_file_hash(),
        "units": {
            "linear": cmds.currentUnit(q=True, linear=True),
            "angle": cmds.currentUnit(q=True, angle=True),
            "time": cmds.currentUnit(q=True, time=True),
        },
        "nodes": _collect_nodes(),
        "transforms": _collect_transforms(),
        "meshes": _collect_meshes(),
        "materials": _collect_materials(),
        "textures": _collect_textures(),
        "cameras": _collect_cameras(),
        "lights": _collect_lights(),
        "joints": _collect_joints(),
        "skins": [],
        "blendShapes": [],
        "animations": [],
        "constraints": [],
        "unsupported": _collect_unsupported(),
    }


def export_scene(output_path: str) -> str:
    _require_maya()
    data = build_scene_dict()
    folder = os.path.dirname(output_path)
    if folder and not os.path.exists(folder):
        os.makedirs(folder)
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2, sort_keys=True)
    print("[MAYAtoUnity] Exported:", output_path)
    return output_path


if __name__ == "__main__":
    _require_maya()
    scene = cmds.file(q=True, sceneName=True) or "untitled"
    base = os.path.splitext(os.path.basename(scene))[0] or "maya_scene"
    out = os.path.join(os.path.expanduser("~"), base + "_maya_to_unity.json")
    export_scene(out)
