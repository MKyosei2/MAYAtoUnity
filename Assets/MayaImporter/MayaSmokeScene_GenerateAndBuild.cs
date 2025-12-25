#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Editor
{
    public static class MayaSmokeScene_GenerateAndBuild
    {
        private const string MenuRoot = "Tools/Maya Importer/Smoke/";
        private const string MenuVisual = MenuRoot + "Build VISUAL Pipeline Smoke Scene (Mesh+Material+Light+Camera)";
        private const string MenuStandard = MenuRoot + "Build STANDARD NodeTypes Smoke Scene (Phase1 UnknownZero)";

        [MenuItem(MenuVisual)]
        public static void BuildVisualPipelineSmoke()
        {
            var scene = new MayaSceneData();
            scene.SetRawAscii("SMOKE__VisualPipeline.ma", text: string.Empty);

            // Root
            var root = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT", "transform", parentName: null);
            MayaSyntheticSceneUtil.SetFloat3(root, ".t", Vector3.zero);
            MayaSyntheticSceneUtil.SetFloat3(root, ".r", Vector3.zero);
            MayaSyntheticSceneUtil.SetFloat3(root, ".s", Vector3.one);

            // Camera (transform + camera)
            var camTr = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT|camera1", "transform", parentName: root.Name);
            MayaSyntheticSceneUtil.SetFloat3(camTr, ".t", new Vector3(0f, 0.75f, -3.0f));
            MayaSyntheticSceneUtil.SetFloat3(camTr, ".r", new Vector3(10f, 0f, 0f));
            MayaSyntheticSceneUtil.SetFloat3(camTr, ".s", Vector3.one);

            var camShape = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT|camera1|cameraShape1", "camera", parentName: camTr.Name);
            MayaSyntheticSceneUtil.SetFloat1(camShape, ".nearClipPlane", 0.01f);
            MayaSyntheticSceneUtil.SetFloat1(camShape, ".farClipPlane", 1000f);
            MayaSyntheticSceneUtil.SetFloat1(camShape, ".focalLength", 35f);

            // Directional light (transform + directionalLight)
            var lightTr = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT|dirLight1", "transform", parentName: root.Name);
            MayaSyntheticSceneUtil.SetFloat3(lightTr, ".t", new Vector3(0f, 2.5f, -1.0f));
            MayaSyntheticSceneUtil.SetFloat3(lightTr, ".r", new Vector3(45f, 30f, 0f));
            MayaSyntheticSceneUtil.SetFloat3(lightTr, ".s", Vector3.one);

            var lightShape = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT|dirLight1|dirLightShape1", "directionalLight", parentName: lightTr.Name);
            MayaSyntheticSceneUtil.SetColor3(lightShape, ".color", Color.white);
            MayaSyntheticSceneUtil.SetFloat1(lightShape, ".intensity", 1.2f);

            // Geometry (transform + mesh)
            var meshTr = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT|pCube1", "transform", parentName: root.Name);
            MayaSyntheticSceneUtil.SetFloat3(meshTr, ".t", Vector3.zero);
            MayaSyntheticSceneUtil.SetFloat3(meshTr, ".r", new Vector3(0f, 20f, 0f));
            MayaSyntheticSceneUtil.SetFloat3(meshTr, ".s", Vector3.one);

            var meshShape = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT|pCube1|pCubeShape1", "mesh", parentName: meshTr.Name);

            // Minimal triangle: vt + fc
            MayaSyntheticSceneUtil.SetAttrTokens(meshShape, ".vt[0:2]",
                "0", "0", "0",
                "1", "0", "0",
                "0", "1", "0");

            MayaSyntheticSceneUtil.SetAttrTokens(meshShape, ".fc[0:0]",
                "f", "3", "0", "1", "2");

            // Shading: lambert + shadingEngine
            var lambert = MayaSyntheticSceneUtil.AddNode(scene, "lambert1", "lambert", parentName: null);
            MayaSyntheticSceneUtil.SetColor3(lambert, ".c", new Color(0.85f, 0.25f, 0.25f, 1f));
            MayaSyntheticSceneUtil.SetColor3(lambert, ".t", Color.black);

            var shadingEngine = MayaSyntheticSceneUtil.AddNode(scene, "initialShadingGroup", "shadingEngine", parentName: null);

            MayaSyntheticSceneUtil.AddConnection(scene,
                srcPlug: "lambert1.outColor",
                dstPlug: "initialShadingGroup.surfaceShader",
                force: true);

            MayaSyntheticSceneUtil.AddRawSetsForceElement(scene, "initialShadingGroup", "pCubeShape1");
            MayaSyntheticSceneUtil.AddConnection(scene,
                srcPlug: "pCubeShape1.instObjGroups[0]",
                dstPlug: "initialShadingGroup.dagSetMembers[0]",
                force: true);

            // Optional texture network
            var place2d = MayaSyntheticSceneUtil.AddNode(scene, "place2dTexture1", "place2dTexture", parentName: null);
            MayaSyntheticSceneUtil.SetFloat2(place2d, ".repeatUV", new Vector2(1f, 1f));
            MayaSyntheticSceneUtil.SetFloat2(place2d, ".offset", new Vector2(0f, 0f));
            MayaSyntheticSceneUtil.SetFloat1(place2d, ".rotateUV", 0f);

            var file = MayaSyntheticSceneUtil.AddNode(scene, "file1", "file", parentName: null);
            MayaSyntheticSceneUtil.SetAttrTokens(file, ".ftn", "SMOKE_Dummy.png");
            MayaSyntheticSceneUtil.SetAttrTokens(file, ".cs", "sRGB");

            MayaSyntheticSceneUtil.AddConnection(scene, "place2dTexture1.outUV", "file1.uvCoord", true);
            MayaSyntheticSceneUtil.AddConnection(scene, "file1.outColor", "lambert1.color", true);

            BuildIntoActiveUnityScene(scene, expectedRootName: "SMOKE__VisualPipeline");
        }

        [MenuItem(MenuStandard)]
        public static void BuildStandardNodeTypesSmoke()
        {
            var scene = new MayaSceneData();
            scene.SetRawAscii("SMOKE__StandardNodeTypes.ma", text: string.Empty);

            HashSet<string> std;
            if (!MayaStandardNodeTypes.TryGet(out std) || std == null || std.Count == 0)
            {
                std = new HashSet<string>(StringComparer.Ordinal)
                {
                    "transform","camera","directionalLight","mesh",
                    "lambert","file","place2dTexture","shadingEngine"
                };
            }

            var root = MayaSyntheticSceneUtil.AddNode(scene, "|SMOKE_ROOT", "transform", parentName: null);
            MayaSyntheticSceneUtil.SetFloat3(root, ".t", Vector3.zero);
            MayaSyntheticSceneUtil.SetFloat3(root, ".r", Vector3.zero);
            MayaSyntheticSceneUtil.SetFloat3(root, ".s", Vector3.one);

            var ordered = std
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Take(280)
                .ToArray();

            for (int i = 0; i < ordered.Length; i++)
            {
                var t = ordered[i];
                var name = $"SMOKE_{t}_{i:D4}";
                var rec = MayaSyntheticSceneUtil.AddNode(scene, name, t, parentName: root.Name);

                if (string.Equals(t, "transform", StringComparison.OrdinalIgnoreCase))
                {
                    MayaSyntheticSceneUtil.SetFloat3(rec, ".t", new Vector3(0, 0, i * 0.05f));
                    MayaSyntheticSceneUtil.SetFloat3(rec, ".r", Vector3.zero);
                    MayaSyntheticSceneUtil.SetFloat3(rec, ".s", Vector3.one);
                }
            }

            BuildIntoActiveUnityScene(scene, expectedRootName: "SMOKE__StandardNodeTypes");
        }

        private static void BuildIntoActiveUnityScene(MayaSceneData scene, string expectedRootName)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            var existing = GameObject.Find(expectedRootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);

            var options = new MayaImportOptions
            {
                CreateUnityComponents = true
            };
            var log = new MayaImportLog();

            var builder = new UnitySceneBuilder(options, log);
            var rootGo = builder.Build(scene);

            rootGo.name = expectedRootName;

            Selection.activeGameObject = rootGo;
            EditorGUIUtility.PingObject(rootGo);

            Debug.Log($"[MayaImporter] ✅ Built SmokeScene: {expectedRootName} (nodes={scene.Nodes.Count}, conns={scene.Connections.Count})");
        }
    }
}
#endif
