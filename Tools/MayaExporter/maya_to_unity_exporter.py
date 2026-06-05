# MAYAtoUnity Maya-side exporter
# Run inside Autodesk Maya's Python environment.
#
# Production-oriented bridge path:
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
from typing import Any, Dict, List, Optional, Tuple

try:
    import maya.cmds as cmds
except Exception:
    cmds = None

try:
    import maya.api.OpenMaya as om
except Exception:
    om = None

SCHEMA_VERSION = 3


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


def _float_attr(node: str, attr: str, default: float = 0.0) -> float:
    try:
        return float(cmds.getAttr(node + "." + attr))
    except Exception:
        return default


def _float3_attr(node: str, attr: str, default: Optional[List[float]] = None) -> List[float]:
    default = default or [0.0, 0.0, 0.0]
    try:
        value = cmds.getAttr(node + "." + attr)
        if isinstance(value, (list, tuple)):
            if len(value) > 0 and isinstance(value[0], (list, tuple)):
                return [float(value[0][0]), float(value[0][1]), float(value[0][2])]
            if len(value) >= 3:
                return [float(value[0]), float(value[1]), float(value[2])]
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
        return [float(x) for x in cmds.xform(node, q=True, matrix=True, objectSpace=True)]
    except Exception:
        return []


def _world_matrix_list(node: str) -> List[float]:
    try:
        return [float(x) for x in cmds.xform(node, q=True, matrix=True, worldSpace=True)]
    except Exception:
        return []


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
        output.append({
            "name": node.split("|")[-1],
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


def _dag_path(shape: str):
    if om is None:
        return None
    try:
        sel = om.MSelectionList()
        sel.add(shape)
        return sel.getDagPath(0)
    except Exception:
        return None


def _try_get_face_uv(poly_iter: Any, local_index: int) -> Tuple[float, float]:
    try:
        uv = poly_iter.getUV(local_index)
        return float(uv[0]), float(uv[1])
    except Exception:
        return 0.0, 0.0


def _try_get_face_normal(poly_iter: Any, local_index: int, fallback: Any = None) -> Tuple[float, float, float]:
    try:
        n = poly_iter.getNormal(local_index, om.MSpace.kObject)
        return float(n.x), float(n.y), float(n.z)
    except Exception:
        try:
            n = fallback if fallback is not None else poly_iter.getNormal(om.MSpace.kObject)
            return float(n.x), float(n.y), float(n.z)
        except Exception:
            return 0.0, 1.0, 0.0


def _export_mesh_topology(shape: str) -> Dict[str, List[float]]:
    result = {"vertices": [], "normals": [], "uvs": [], "indices": []}
    dag = _dag_path(shape)
    if dag is None or om is None:
        return result
    try:
        fn = om.MFnMesh(dag)
        points = fn.getPoints(om.MSpace.kObject)
        it = om.MItMeshPolygon(dag)
    except Exception:
        return result

    next_index = 0
    while not it.isDone():
        try:
            count = it.polygonVertexCount()
        except Exception:
            count = 0
        if count >= 3:
            try:
                face_normal = it.getNormal(om.MSpace.kObject)
            except Exception:
                face_normal = None
            for i in range(1, count - 1):
                for local in [0, i, i + 1]:
                    try:
                        vertex_id = it.vertexIndex(local)
                        p = points[vertex_id]
                    except Exception:
                        continue
                    nx, ny, nz = _try_get_face_normal(it, local, face_normal)
                    u, v = _try_get_face_uv(it, local)
                    result["vertices"].extend([float(p.x), float(p.y), float(p.z)])
                    result["normals"].extend([nx, ny, nz])
                    result["uvs"].extend([u, v])
                    result["indices"].append(next_index)
                    next_index += 1
        try:
            it.next()
        except Exception:
            break
    return result


def _mesh_materials(shape: str) -> List[str]:
    try:
        sgs = cmds.listConnections(shape, type="shadingEngine") or []
        return sorted(set(sgs))
    except Exception:
        return []


def _collect_meshes() -> List[Dict[str, Any]]:
    meshes = []
    for shape in sorted(cmds.ls(type="mesh", long=True) or []):
        topology = _export_mesh_topology(shape)
        mesh_data: Dict[str, Any] = {
            "name": shape.split("|")[-1],
            "path": shape,
            "parentPath": _parent_path(shape),
            "uuid": _node_uuid(shape),
            "sourceVertexCount": int(cmds.polyEvaluate(shape, vertex=True) or 0),
            "sourceTriangleCount": int(cmds.polyEvaluate(shape, triangle=True) or 0),
            "vertices": topology.get("vertices", []),
            "normals": topology.get("normals", []),
            "uvs": topology.get("uvs", []),
            "indices": topology.get("indices", []),
            "materials": _mesh_materials(shape),
        }
        mesh_data["vertexCount"] = len(mesh_data["vertices"]) // 3
        mesh_data["triangleCount"] = len(mesh_data["indices"]) // 3
        meshes.append(mesh_data)
    return meshes


def _first_connected_file_texture(material: str) -> str:
    try:
        nodes = cmds.listHistory(material, future=False, pruneDagObjects=True) or []
        for n in nodes:
            try:
                if cmds.nodeType(n) == "file":
                    return _safe_get_attr(n + ".fileTextureName", "") or _safe_get_attr(n + ".ftn", "") or ""
            except Exception:
                pass
    except Exception:
        pass
    return ""


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
                "color": _float3_attr(node, "color", [1.0, 1.0, 1.0]),
                "transparency": _float3_attr(node, "transparency", [0.0, 0.0, 0.0]),
                "diffuseTexture": _first_connected_file_texture(node),
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
            "focalLength": _float_attr(shape, "focalLength", 35.0),
            "horizontalFilmAperture": _float_attr(shape, "horizontalFilmAperture", 1.417),
            "verticalFilmAperture": _float_attr(shape, "verticalFilmAperture", 0.945),
            "nearClipPlane": _float_attr(shape, "nearClipPlane", 0.1),
            "farClipPlane": _float_attr(shape, "farClipPlane", 1000.0),
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
                "color": _float3_attr(shape, "color", [1.0, 1.0, 1.0]),
                "intensity": _float_attr(shape, "intensity", 1.0),
                "coneAngle": _float_attr(shape, "coneAngle", 40.0),
                "penumbraAngle": _float_attr(shape, "penumbraAngle", 0.0),
                "dropoff": _float_attr(shape, "dropoff", 0.0),
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


def _unity_property_from_attr(attr: str) -> str:
    table = {
        "translateX": "localPosition.x", "translateY": "localPosition.y", "translateZ": "localPosition.z",
        "rotateX": "localEulerAnglesRaw.x", "rotateY": "localEulerAnglesRaw.y", "rotateZ": "localEulerAnglesRaw.z",
        "scaleX": "localScale.x", "scaleY": "localScale.y", "scaleZ": "localScale.z",
    }
    return table.get(attr, "")


def _collect_animation_curves() -> List[Dict[str, Any]]:
    curves = []
    for curve in sorted(cmds.ls(type=["animCurveTL", "animCurveTA", "animCurveTU"]) or []):
        try:
            destinations = cmds.listConnections(curve + ".output", plugs=True, destination=True, source=False) or []
        except Exception:
            destinations = []
        if not destinations:
            continue
        dst = destinations[0]
        if "." not in dst:
            continue
        target, attr = dst.split(".", 1)
        unity_prop = _unity_property_from_attr(attr)
        if not unity_prop:
            continue
        try:
            key_count = cmds.keyframe(curve, q=True, keyframeCount=True) or 0
            times = cmds.keyframe(curve, q=True, index=(0, key_count - 1), timeChange=True) or []
            values = cmds.keyframe(curve, q=True, index=(0, key_count - 1), valueChange=True) or []
        except Exception:
            times, values = [], []
        curves.append({
            "targetPath": _full_path(target),
            "attribute": attr,
            "unityProperty": unity_prop,
            "times": [float(x) for x in times],
            "values": [float(x) for x in values],
        })
    return curves


def _collect_unsupported() -> List[Dict[str, Any]]:
    supported = {
        "transform", "joint", "mesh", "camera",
        "ambientLight", "directionalLight", "pointLight", "spotLight", "areaLight",
        "lambert", "phong", "blinn", "surfaceShader", "aiStandardSurface", "standardSurface",
        "file", "place2dTexture", "shadingEngine", "skinCluster", "blendShape",
        "animCurveTL", "animCurveTA", "animCurveTU",
    }
    unsupported = []
    for node in sorted(cmds.ls() or []):
        try:
            t = cmds.nodeType(node)
        except Exception:
            t = "unknown"
        if t not in supported:
            unsupported.append({"name": node, "type": t, "reason": "not in exporter v3 support set"})
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
        "animations": _collect_animation_curves(),
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
