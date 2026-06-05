// MAYAIMPORTER_PATCH_V10: Convert Maya exporter JSON DTO to MayaSceneData
using System;
using System.Collections.Generic;

namespace MayaImporter.Core
{
    public static class MayaUnityJsonSceneConverter
    {
        public static MayaSceneData Convert(string sourcePath, string rawJson, MayaUnityExport export, MayaImportLog log)
        {
            var scene = new MayaSceneData();
            scene.SetRawAscii(sourcePath, rawJson ?? string.Empty);
            if (export != null && !string.IsNullOrEmpty(export.sourceHash)) scene.RawSha256 = export.sourceHash;

            if (export == null)
            {
                log?.Error("MayaUnityExport is null.");
                return scene;
            }

            if (export.units != null)
            {
                PutUnit(scene, "linear", export.units.linear);
                PutUnit(scene, "angle", export.units.angle);
                PutUnit(scene, "time", export.units.time);
            }

            AddGenericNodes(scene, export.nodes);
            AddTransforms(scene, export.transforms);
            AddMeshes(scene, export.meshes);
            AddMaterials(scene, export.materials);
            AddTextures(scene, export.textures);
            AddCameras(scene, export.cameras);
            AddLights(scene, export.lights);
            AddJoints(scene, export.joints);
            AddAnimationCurves(scene, export.animations);
            AddConstraints(scene, export.constraints);
            AddUnsupported(scene, export.unsupported);

            log?.Info("Maya exporter JSON converted. nodes=" + scene.Nodes.Count + " connections=" + scene.Connections.Count);
            return scene;
        }

        private static void AddGenericNodes(MayaSceneData scene, MayaUnityExportNode[] nodes)
        {
            if (nodes == null) return;
            foreach (var n in nodes)
            {
                string name = StableName(n.path, n.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, string.IsNullOrEmpty(n.type) ? "unknown" : n.type);
                rec.Uuid = n.uuid;
                rec.ParentName = StableName(n.parentPath, null);
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:nodes";
            }
        }

        private static void AddTransforms(MayaSceneData scene, MayaUnityExportTransform[] transforms)
        {
            if (transforms == null) return;
            foreach (var t in transforms)
            {
                string name = StableName(t.path, t.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, "transform");
                rec.Uuid = t.uuid;
                rec.ParentName = StableName(t.parentPath, null);
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:transforms";
                AddFloatArrayAttr(rec, ".t", t.localPosition, "double3");
                AddFloatArrayAttr(rec, ".r", t.localRotation, "double3");
                AddFloatArrayAttr(rec, ".s", t.localScale, "double3");
                AddFloatArrayAttr(rec, ".matrix", t.localMatrix, "matrix");
                AddFloatArrayAttr(rec, ".worldMatrix", t.worldMatrix, "matrix");
            }
        }

        private static void AddMeshes(MayaSceneData scene, MayaUnityExportMesh[] meshes)
        {
            if (meshes == null) return;
            foreach (var m in meshes)
            {
                string name = StableName(m.path, m.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, "mesh");
                rec.Uuid = m.uuid;
                rec.ParentName = StableName(m.parentPath, null);
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = MayaUnityJsonMeshBuilder.HasTopology(m) ? "MayaExporterJson:meshes:topology" : "MayaExporterJson:meshes:metadata";
                AddIntAttr(rec, ".vertexCount", m.vertexCount);
                AddIntAttr(rec, ".triangleCount", m.triangleCount);
                AddIntAttr(rec, ".sourceVertexCount", m.sourceVertexCount);
                AddIntAttr(rec, ".sourceTriangleCount", m.sourceTriangleCount);
                AddIntAttr(rec, ".exportedVertexFloatCount", m.vertices != null ? m.vertices.Length : 0);
                AddIntAttr(rec, ".exportedIndexCount", m.indices != null ? m.indices.Length : 0);
                AddIntAttr(rec, ".subMeshCount", m.subMeshes != null ? m.subMeshes.Length : 0);
                AddIntAttr(rec, ".blendShapeCount", m.blendShapes != null ? m.blendShapes.Length : 0);
                AddIntAttr(rec, ".skinJointCount", m.skinJoints != null ? m.skinJoints.Length : 0);

                if (m.materials == null) continue;
                for (int i = 0; i < m.materials.Length; i++)
                {
                    string material = m.materials[i];
                    if (string.IsNullOrEmpty(material)) continue;
                    scene.Connections.Add(new ConnectionRecord(name + ".instObjGroups[" + i + "]", material + ".dagSetMembers[" + i + "]"));
                }
            }
        }

        private static void AddMaterials(MayaSceneData scene, MayaUnityExportMaterial[] materials)
        {
            if (materials == null) return;
            foreach (var mat in materials)
            {
                string name = StableName(null, mat.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, string.IsNullOrEmpty(mat.type) ? "material" : mat.type);
                rec.Uuid = mat.uuid;
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:materials";
                AddFloatArrayAttr(rec, ".exportedColor", mat.color, "float3");
                AddFloatArrayAttr(rec, ".exportedTransparency", mat.transparency, "float3");
                AddStringAttr(rec, ".diffuseTexture", mat.diffuseTexture);
            }
        }

        private static void AddTextures(MayaSceneData scene, MayaUnityExportTexture[] textures)
        {
            if (textures == null) return;
            foreach (var tex in textures)
            {
                string name = StableName(null, tex.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, string.IsNullOrEmpty(tex.type) ? "file" : tex.type);
                rec.Uuid = tex.uuid;
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:textures";
                AddStringAttr(rec, ".ftn", tex.fileTextureName);
                AddStringAttr(rec, ".colorSpace", tex.colorSpace);
            }
        }

        private static void AddCameras(MayaSceneData scene, MayaUnityExportCamera[] cameras)
        {
            if (cameras == null) return;
            foreach (var cam in cameras)
            {
                string name = StableName(cam.path, cam.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, "camera");
                rec.Uuid = cam.uuid;
                rec.ParentName = StableName(cam.parentPath, null);
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:cameras";
                AddFloatAttr(rec, ".focalLength", cam.focalLength);
                AddFloatAttr(rec, ".horizontalFilmAperture", cam.horizontalFilmAperture);
                AddFloatAttr(rec, ".verticalFilmAperture", cam.verticalFilmAperture);
                AddFloatAttr(rec, ".nearClipPlane", cam.nearClipPlane);
                AddFloatAttr(rec, ".farClipPlane", cam.farClipPlane);
            }
        }

        private static void AddLights(MayaSceneData scene, MayaUnityExportLight[] lights)
        {
            if (lights == null) return;
            foreach (var light in lights)
            {
                string name = StableName(light.path, light.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, string.IsNullOrEmpty(light.type) ? "light" : light.type);
                rec.Uuid = light.uuid;
                rec.ParentName = StableName(light.parentPath, null);
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:lights";
                AddFloatArrayAttr(rec, ".color", light.color, "float3");
                AddFloatAttr(rec, ".intensity", light.intensity);
                AddFloatAttr(rec, ".coneAngle", light.coneAngle);
                AddFloatAttr(rec, ".penumbraAngle", light.penumbraAngle);
                AddFloatAttr(rec, ".dropoff", light.dropoff);
            }
        }

        private static void AddJoints(MayaSceneData scene, MayaUnityExportJoint[] joints)
        {
            if (joints == null) return;
            foreach (var joint in joints)
            {
                string name = StableName(joint.path, joint.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, "joint");
                rec.Uuid = joint.uuid;
                rec.ParentName = StableName(joint.parentPath, null);
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:joints";
                AddFloatArrayAttr(rec, ".t", joint.localPosition, "double3");
                AddFloatArrayAttr(rec, ".r", joint.localRotation, "double3");
                AddFloatArrayAttr(rec, ".s", joint.localScale, "double3");
                AddFloatArrayAttr(rec, ".jo", joint.jointOrient, "double3");
                AddFloatArrayAttr(rec, ".worldMatrix", joint.worldMatrix, "matrix");
            }
        }

        private static void AddAnimationCurves(MayaSceneData scene, MayaUnityExportAnimationCurve[] animations)
        {
            if (animations == null) return;
            for (int i = 0; i < animations.Length; i++)
            {
                var a = animations[i];
                if (a == null || string.IsNullOrEmpty(a.targetPath)) continue;
                var rec = scene.GetOrCreateNode("__jsonAnimCurve_" + i, "animCurveJson");
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:animations";
                AddStringAttr(rec, ".targetPath", a.targetPath);
                AddStringAttr(rec, ".attribute", a.attribute);
                AddStringAttr(rec, ".unityProperty", a.unityProperty);
                AddFloatArrayAttr(rec, ".times", a.times, "floatArray");
                AddFloatArrayAttr(rec, ".values", a.values, "floatArray");
            }
        }

        private static void AddConstraints(MayaSceneData scene, MayaUnityExportConstraint[] constraints)
        {
            if (constraints == null) return;
            foreach (var c in constraints)
            {
                string name = StableName(c.path, c.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, string.IsNullOrEmpty(c.type) ? "constraint" : c.type);
                rec.Uuid = c.uuid;
                rec.ParentName = StableName(c.parentPath, null);
                rec.Provenance = MayaNodeProvenance.AsciiCommands;
                rec.ProvenanceDetail = "MayaExporterJson:constraints:bakedToAnimation";
                AddStringAttr(rec, ".bakeStatus", "bakedToAnimationCurves");
            }
        }

        private static void AddUnsupported(MayaSceneData scene, MayaUnityExportUnsupported[] unsupported)
        {
            if (unsupported == null) return;
            foreach (var item in unsupported)
            {
                string name = StableName(null, item.name);
                if (string.IsNullOrEmpty(name)) continue;
                var rec = scene.GetOrCreateNode(name, string.IsNullOrEmpty(item.type) ? "unknown" : item.type);
                rec.Provenance = MayaNodeProvenance.MbHeuristic;
                rec.ProvenanceDetail = "MayaExporterJson:unsupported:" + item.reason;
                AddStringAttr(rec, ".unsupportedReason", item.reason);
            }
        }

        private static void PutUnit(MayaSceneData scene, string key, string value)
        {
            if (scene == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
            scene.SceneUnits[key] = value;
        }

        private static string StableName(string path, string name)
        {
            if (!string.IsNullOrEmpty(path)) return path.Trim().Trim('|');
            return string.IsNullOrEmpty(name) ? string.Empty : name.Trim();
        }

        private static void AddStringAttr(NodeRecord rec, string key, string value)
        {
            if (rec == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
            var attr = new RawAttributeValue("string", new List<string> { value });
            attr.Kind = MayaAttrValueKind.StringArray;
            attr.ParsedValue = new[] { value };
            rec.Attributes[key] = attr;
        }

        private static void AddFloatAttr(NodeRecord rec, string key, float value)
        {
            if (rec == null || string.IsNullOrEmpty(key)) return;
            var attr = new RawAttributeValue("float", new List<string> { value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            attr.Kind = MayaAttrValueKind.Float;
            attr.ParsedValue = value;
            rec.Attributes[key] = attr;
        }

        private static void AddIntAttr(NodeRecord rec, string key, int value)
        {
            if (rec == null || string.IsNullOrEmpty(key)) return;
            var attr = new RawAttributeValue("int", new List<string> { value.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            attr.Kind = MayaAttrValueKind.Int;
            attr.ParsedValue = value;
            rec.Attributes[key] = attr;
        }

        private static void AddFloatArrayAttr(NodeRecord rec, string key, float[] values, string typeName)
        {
            if (rec == null || string.IsNullOrEmpty(key) || values == null || values.Length == 0) return;
            var tokens = new List<string>(values.Length);
            for (int i = 0; i < values.Length; i++) tokens.Add(values[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
            var attr = new RawAttributeValue(typeName, tokens);
            attr.Kind = values.Length == 16 ? MayaAttrValueKind.Matrix4x4 : (values.Length == 4 ? MayaAttrValueKind.Vector4 : (values.Length == 3 ? MayaAttrValueKind.Vector3 : MayaAttrValueKind.FloatArray));
            attr.ParsedValue = values;
            rec.Attributes[key] = attr;
        }
    }
}
