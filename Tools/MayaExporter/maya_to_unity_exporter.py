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
from typing import Any, Dict, List, Optional, Tuple, Set

try:
    import maya.cmds as cmds
except Exception:
    cmds = None

try:
    import maya.api.OpenMaya as om
except Exception:
    om = None

SCHEMA_VERSION = 9
CONSTRAINT_TYPES = ["parentConstraint", "pointConstraint", "orientConstraint", "scaleConstraint", "aimConstraint"]


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


def _world_inverse_matrix_list(node: str) -> List[float]:
    try:
        world = _world_matrix_list(node)
        if len(world) != 16 or om is None:
            return []
        inv = om.MMatrix(world).inverse()
        return [float(inv[i]) for i in range(16)]
    except Exception:
        return []


def _flatten_matrix_value(value: Any) -> List[float]:
    if value is None:
        return []
    try:
        if isinstance(value, (list, tuple)) and len(value) == 1 and isinstance(value[0], (list, tuple)):
            value = value[0]
        if isinstance(value, (list, tuple)) and len(value) == 16:
            return [float(x) for x in value]
    except Exception:
        pass
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


def _mesh_points(shape: str) -> List[Any]:
    dag = _dag_path(shape)
    if dag is None or om is None:
        return []
    try:
        return list(om.MFnMesh(dag).getPoints(om.MSpace.kObject))
    except Exception:
        return []


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


def _export_mesh_topology(shape: str) -> Dict[str, Any]:
    result: Dict[str, Any] = {
        "vertices": [], "normals": [], "uvs": [], "indices": [],
        "sourceVertexIndices": [], "triangleFaceIndices": []
    }
    dag = _dag_path(shape)
    if dag is None or om is None:
        return result
    try:
        points = om.MFnMesh(dag).getPoints(om.MSpace.kObject)
        it = om.MItMeshPolygon(dag)
    except Exception:
        return result

    next_index = 0
    while not it.isDone():
        try:
            count = it.polygonVertexCount()
            face_id = int(it.index())
        except Exception:
            count = 0
            face_id = -1
        if count >= 3:
            try:
                face_normal = it.getNormal(om.MSpace.kObject)
            except Exception:
                face_normal = None
            for i in range(1, count - 1):
                tri_indices: List[int] = []
                for local in [0, i, i + 1]:
                    try:
                        vertex_id = int(it.vertexIndex(local))
                        p = points[vertex_id]
                    except Exception:
                        continue
                    nx, ny, nz = _try_get_face_normal(it, local, face_normal)
                    u, v = _try_get_face_uv(it, local)
                    result["vertices"].extend([float(p.x), float(p.y), float(p.z)])
                    result["normals"].extend([nx, ny, nz])
                    result["uvs"].extend([u, v])
                    result["indices"].append(next_index)
                    result["sourceVertexIndices"].append(vertex_id)
                    tri_indices.append(next_index)
                    next_index += 1
                if len(tri_indices) == 3:
                    result["triangleFaceIndices"].append(face_id)
        try:
            it.next()
        except Exception:
            break
    return result


def _shader_from_shading_engine(sg: str) -> str:
    try:
        shaders = cmds.listConnections(sg + ".surfaceShader", source=True, destination=False) or []
        if shaders:
            return shaders[0]
    except Exception:
        pass
    return sg


def _shape_shading_engines(shape: str) -> List[str]:
    try:
        return sorted(set(cmds.listConnections(shape, type="shadingEngine") or []))
    except Exception:
        return []


def _face_material_map(shape: str, shading_engines: List[str]) -> Dict[int, str]:
    face_to_material: Dict[int, str] = {}
    for sg in shading_engines:
        material = _shader_from_shading_engine(sg)
        try:
            members = cmds.sets(sg, q=True) or []
        except Exception:
            members = []
        for member in members:
            try:
                faces = cmds.ls(member, flatten=True) or []
            except Exception:
                faces = []
            for f in faces:
                if ".f[" not in f:
                    continue
                if not f.startswith(shape) and not f.startswith(shape.split("|")[-1]):
                    continue
                try:
                    inside = f.split(".f[")[-1].split("]")[0]
                    if ":" in inside:
                        a, b = inside.split(":", 1)
                        for idx in range(int(a), int(b) + 1):
                            face_to_material[idx] = material
                    else:
                        face_to_material[int(inside)] = material
                except Exception:
                    pass
    return face_to_material


def _build_submeshes(topology: Dict[str, Any], materials: List[str], face_materials: Dict[int, str]) -> List[Dict[str, Any]]:
    if not materials:
        return []
    by_material: Dict[str, List[int]] = {m: [] for m in materials}
    fallback = materials[0]
    tri_faces = topology.get("triangleFaceIndices", []) or []
    indices = topology.get("indices", []) or []
    tri_count = len(indices) // 3
    for tri in range(tri_count):
        face = tri_faces[tri] if tri < len(tri_faces) else -1
        material = face_materials.get(face, fallback)
        if material not in by_material:
            by_material[material] = []
        o = tri * 3
        by_material[material].extend([indices[o + 0], indices[o + 1], indices[o + 2]])
    return [{"material": m, "indices": idx} for m, idx in by_material.items() if idx]


def _skin_cluster_for_shape(shape: str) -> str:
    try:
        history = cmds.listHistory(shape, pruneDagObjects=True) or []
        for node in history:
            if cmds.nodeType(node) == "skinCluster":
                return node
    except Exception:
        pass
    return ""


def _bindpose_matrix_for_influence(skin: str, joint: str, influence_order: int) -> List[float]:
    for candidate in (influence_order,):
        try:
            value = cmds.getAttr(f"{skin}.bindPreMatrix[{candidate}]")
            flat = _flatten_matrix_value(value)
            if len(flat) == 16:
                return flat
        except Exception:
            pass

    fallback = _world_inverse_matrix_list(joint)
    if len(fallback) == 16:
        return fallback

    return [1.0, 0.0, 0.0, 0.0,
            0.0, 1.0, 0.0, 0.0,
            0.0, 0.0, 1.0, 0.0,
            0.0, 0.0, 0.0, 1.0]


def _bindposes_for_skin(skin: str, influences: List[str]) -> List[float]:
    bindposes: List[float] = []
    for i, joint in enumerate(influences):
        bindposes.extend(_bindpose_matrix_for_influence(skin, joint, i))
    return bindposes


def _skin_data_for_shape(shape: str, topology: Dict[str, Any]) -> Dict[str, Any]:
    skin = _skin_cluster_for_shape(shape)
    if not skin:
        return {"skinCluster": "", "skinJoints": [], "boneIndices": [], "boneWeights": [], "bindposes": []}

    try:
        influences = cmds.skinCluster(skin, q=True, influence=True) or []
    except Exception:
        influences = []
    if not influences:
        return {"skinCluster": skin, "skinJoints": [], "boneIndices": [], "boneWeights": [], "bindposes": []}

    source_indices = topology.get("sourceVertexIndices", []) or []
    bone_indices: List[int] = []
    bone_weights: List[float] = []
    cache: Dict[int, List[Tuple[int, float]]] = {}

    for vertex_id in source_indices:
        vertex_id = int(vertex_id)
        if vertex_id not in cache:
            pairs: List[Tuple[int, float]] = []
            component = f"{shape}.vtx[{vertex_id}]"
            for i, joint in enumerate(influences):
                try:
                    w = float(cmds.skinPercent(skin, component, q=True, transform=joint))
                except Exception:
                    w = 0.0
                if w > 0.000001:
                    pairs.append((i, w))
            pairs.sort(key=lambda p: p[1], reverse=True)
            pairs = pairs[:4]
            total = sum(w for _, w in pairs)
            if total <= 0.0:
                pairs = [(0, 1.0)]
                total = 1.0
            pairs = [(i, w / total) for i, w in pairs]
            while len(pairs) < 4:
                pairs.append((0, 0.0))
            cache[vertex_id] = pairs
        for i, w in cache[vertex_id]:
            bone_indices.append(int(i))
            bone_weights.append(float(w))

    return {
        "skinCluster": skin,
        "skinJoints": [_full_path(j) for j in influences],
        "boneIndices": bone_indices,
        "boneWeights": bone_weights,
        "bindposes": _bindposes_for_skin(skin, influences),
    }


def _blendshape_nodes_for_shape(shape: str) -> List[str]:
    try:
        history = cmds.listHistory(shape, pruneDagObjects=True) or []
        return [node for node in history if cmds.nodeType(node) == "blendShape"]
    except Exception:
        return []


def _blendshape_aliases(node: str) -> List[str]:
    aliases: List[str] = []
    try:
        raw = cmds.aliasAttr(node, q=True) or []
        for i in range(0, len(raw), 2):
            alias = raw[i]
            if alias:
                aliases.append(alias)
    except Exception:
        pass
    return aliases


def _capture_exported_positions(shape: str, source_indices: List[int]) -> List[Tuple[float, float, float]]:
    points = _mesh_points(shape)
    output: List[Tuple[float, float, float]] = []
    for vertex_id in source_indices:
        try:
            p = points[int(vertex_id)]
            output.append((float(p.x), float(p.y), float(p.z)))
        except Exception:
            output.append((0.0, 0.0, 0.0))
    return output


def _force_evaluate() -> None:
    try:
        cmds.dgdirty(allPlugs=True)
    except Exception:
        pass
    try:
        cmds.refresh(force=True)
    except Exception:
        pass


def _all_blendshape_weight_plugs(nodes: List[str]) -> List[str]:
    plugs: List[str] = []
    for node in nodes:
        for alias in _blendshape_aliases(node):
            plug = node + "." + alias
            try:
                if cmds.objExists(plug):
                    plugs.append(plug)
            except Exception:
                pass
    return plugs


def _blendshape_data_for_shape(shape: str, topology: Dict[str, Any]) -> List[Dict[str, Any]]:
    output: List[Dict[str, Any]] = []
    source_indices = topology.get("sourceVertexIndices", []) or []
    if not source_indices:
        return output

    nodes = _blendshape_nodes_for_shape(shape)
    if not nodes:
        return output

    plugs = _all_blendshape_weight_plugs(nodes)
    original_weights: Dict[str, float] = {}
    for plug in plugs:
        try:
            original_weights[plug] = float(cmds.getAttr(plug))
        except Exception:
            original_weights[plug] = 0.0

    try:
        for plug in plugs:
            try:
                cmds.setAttr(plug, 0.0)
            except Exception:
                pass
        _force_evaluate()
        neutral = _capture_exported_positions(shape, source_indices)

        for node in nodes:
            for alias in _blendshape_aliases(node):
                plug = node + "." + alias
                if plug not in original_weights:
                    continue
                try:
                    for p in plugs:
                        try:
                            cmds.setAttr(p, 0.0)
                        except Exception:
                            pass
                    cmds.setAttr(plug, 1.0)
                    _force_evaluate()
                    target = _capture_exported_positions(shape, source_indices)
                except Exception:
                    target = neutral

                delta_vertices: List[float] = []
                for i in range(min(len(neutral), len(target))):
                    delta_vertices.extend([
                        target[i][0] - neutral[i][0],
                        target[i][1] - neutral[i][1],
                        target[i][2] - neutral[i][2],
                    ])

                current = original_weights.get(plug, 0.0)
                output.append({
                    "name": alias or node,
                    "weight": 100.0,
                    "currentWeight": float(current) * 100.0,
                    "deltaVertices": delta_vertices,
                    "deltaNormals": [],
                    "deltaTangents": [],
                })
    finally:
        for plug, value in original_weights.items():
            try:
                cmds.setAttr(plug, value)
            except Exception:
                pass
        _force_evaluate()

    return output


def _collect_meshes() -> List[Dict[str, Any]]:
    meshes = []
    for shape in sorted(cmds.ls(type="mesh", long=True) or []):
        topology = _export_mesh_topology(shape)
        shading_engines = _shape_shading_engines(shape)
        materials = [_shader_from_shading_engine(sg) for sg in shading_engines]
        materials = [m for m in materials if m]
        face_materials = _face_material_map(shape, shading_engines)
        skin = _skin_data_for_shape(shape, topology)
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
            "sourceVertexIndices": topology.get("sourceVertexIndices", []),
            "triangleFaceIndices": topology.get("triangleFaceIndices", []),
            "materials": materials,
            "subMeshes": _build_submeshes(topology, materials, face_materials),
            "skinCluster": skin.get("skinCluster", ""),
            "skinJoints": skin.get("skinJoints", []),
            "boneIndices": skin.get("boneIndices", []),
            "boneWeights": skin.get("boneWeights", []),
            "bindposes": skin.get("bindposes", []),
            "blendShapes": _blendshape_data_for_shape(shape, topology),
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


def _playback_times() -> List[float]:
    try:
        start = int(cmds.playbackOptions(q=True, min=True))
        end = int(cmds.playbackOptions(q=True, max=True))
    except Exception:
        start, end = 1, 1
    if end < start:
        start, end = end, start
    return [float(t) for t in range(start, end + 1)]


def _constraint_nodes() -> List[str]:
    nodes: List[str] = []
    for t in CONSTRAINT_TYPES:
        try:
            nodes.extend(cmds.ls(type=t) or [])
        except Exception:
            pass
    return sorted(set(nodes))


def _constraint_driven_transforms() -> List[str]:
    driven: Set[str] = set()
    for constraint in _constraint_nodes():
        try:
            outputs = cmds.listConnections(constraint, plugs=True, source=False, destination=True) or []
        except Exception:
            outputs = []
        for plug in outputs:
            if "." not in plug:
                continue
            node, attr = plug.split(".", 1)
            if attr not in ("translate", "rotate", "scale", "translateX", "translateY", "translateZ", "rotateX", "rotateY", "rotateZ", "scaleX", "scaleY", "scaleZ"):
                continue
            try:
                if cmds.nodeType(node) == "transform":
                    driven.add(_full_path(node))
            except Exception:
                pass
    return sorted(driven)


def _sample_attr(node: str, attr: str) -> float:
    try:
        return float(cmds.getAttr(node + "." + attr))
    except Exception:
        return 0.0


def _collect_constraint_baked_animation_curves() -> List[Dict[str, Any]]:
    driven = _constraint_driven_transforms()
    if not driven:
        return []

    times = _playback_times()
    if not times:
        return []

    try:
        original_time = float(cmds.currentTime(q=True))
    except Exception:
        original_time = times[0]

    attrs = ["translateX", "translateY", "translateZ", "rotateX", "rotateY", "rotateZ", "scaleX", "scaleY", "scaleZ"]
    curves: List[Dict[str, Any]] = []
    try:
        sampled: Dict[Tuple[str, str], List[float]] = {(node, attr): [] for node in driven for attr in attrs}
        for t in times:
            try:
                cmds.currentTime(t, edit=True)
            except Exception:
                pass
            _force_evaluate()
            for node in driven:
                for attr in attrs:
                    sampled[(node, attr)].append(_sample_attr(node, attr))

        for node in driven:
            for attr in attrs:
                unity_prop = _unity_property_from_attr(attr)
                if not unity_prop:
                    continue
                values = sampled.get((node, attr), [])
                if not values:
                    continue
                curves.append({
                    "targetPath": _full_path(node),
                    "attribute": "bakedConstraint." + attr,
                    "unityProperty": unity_prop,
                    "times": times,
                    "values": values,
                })
    finally:
        try:
            cmds.currentTime(original_time, edit=True)
        except Exception:
            pass
        _force_evaluate()

    return curves


def _collect_constraints() -> List[Dict[str, Any]]:
    output: List[Dict[str, Any]] = []
    for node in _constraint_nodes():
        output.append({
            "name": node,
            "path": _full_path(node),
            "type": cmds.nodeType(node),
            "uuid": _node_uuid(node),
            "parentPath": _parent_path(node),
        })
    return output


def _collect_unsupported() -> List[Dict[str, Any]]:
    supported = {
        "transform", "joint", "mesh", "camera",
        "ambientLight", "directionalLight", "pointLight", "spotLight", "areaLight",
        "lambert", "phong", "blinn", "surfaceShader", "aiStandardSurface", "standardSurface",
        "file", "place2dTexture", "shadingEngine", "skinCluster", "blendShape",
        "animCurveTL", "animCurveTA", "animCurveTU",
        "parentConstraint", "pointConstraint", "orientConstraint", "scaleConstraint", "aimConstraint",
    }
    unsupported = []
    for node in sorted(cmds.ls() or []):
        try:
            t = cmds.nodeType(node)
        except Exception:
            t = "unknown"
        if t not in supported:
            unsupported.append({"name": node, "type": t, "reason": "not in exporter v9 support set"})
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
    native_curves = _collect_animation_curves()
    baked_constraint_curves = _collect_constraint_baked_animation_curves()
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
        "animations": native_curves + baked_constraint_curves,
        "constraints": _collect_constraints(),
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
