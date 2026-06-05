# MAYAtoUnity

**MAYAtoUnity** は、Maya / DCC データを Unity 側で検証・再構築するための **Importer / DCC Pipeline Tool** です。

このプロジェクトは、単に Maya ファイルを読み込むだけではなく、Maya のノード、属性、接続、階層、Mesh、Material、Animation、Skin、BlendShape、Constraint の変換根拠を Unity 上で追跡可能な形に残すことを重視しています。

> このリポジトリには 2 つの経路があります。  
> 1. Unity-only `.ma/.mb` preservation / recovery importer  
> 2. Maya 上で実行する exporter から JSON を出し、Unity で高精度に再構築する JSON Bridge

---

## What this project demonstrates

MAYAtoUnity は、ゲーム開発における DCC → Engine 連携を想定した Technical Artist / Tools Programmer 向けのポートフォリオ作品です。

示せる技術要素:

- Unity Editor tooling
- DCC pipeline engineering
- Scene graph reconstruction
- Intermediate data model design
- Mesh topology import
- SubMesh / face material assignment
- Material / texture binding
- Camera / Light reconstruction
- AnimationCurve generation
- Constraint baking
- Skin weight / bindpose import
- BlendShape delta import
- Import report / validation workflow
- Maya Binary best-effort recovery
- Fallback / graceful degradation design

---

## Pipeline overview

```text
Maya scene
  ↓
Tools/MayaExporter/maya_to_unity_exporter.py
  ↓
Exporter JSON schema v9
  ↓
MayaUnityJsonImporter
  ↓
MayaSceneData
  ↓
UnitySceneBuilder
  ↓
GameObject hierarchy
  ↓
Runtime attachment layer
  ↓
Mesh / SubMesh / Material / Camera / Light / Animation / Skin / BlendShape
  ↓
Import Report + Validation
```

---

## Current JSON Bridge support

| Area | Status | Notes |
|---|---:|---|
| Transform hierarchy | Implemented | Maya path / parent path から Unity hierarchy を構築 |
| Mesh topology | Implemented | vertices / normals / uvs / triangle indices をJSON経由で再構築 |
| SubMesh | Implemented | Maya face material assignment を Unity subMesh に変換 |
| Material color | Implemented | lambert / phong / blinn / standardSurface などから基本色を反映 |
| Texture binding | Implemented | file texture path を Unity Material の `_BaseMap` / `_MainTex` へ割り当て |
| Camera | Implemented | focal length / clipping plane / film aperture metadata を保持 |
| Light | Implemented | directional / point / spot / area light metadata を保持・反映 |
| Native animation curves | Implemented | translate / rotate / scale animCurve を Unity AnimationClip 化 |
| Constraint baking | Implemented | parent / point / orient / scale / aim constraint の評価結果を Transform animation にbake |
| Joints | Implemented | joint hierarchy / jointOrient / matrix metadata を保持 |
| Skin weights | Implemented | top 4 weights per exported vertex を Unity BoneWeight に変換 |
| Bindposes | Implemented | skinCluster.bindPreMatrix を優先し、失敗時は joint inverse matrix fallback |
| SkinnedMeshRenderer | Implemented | skin情報ありmeshを SkinnedMeshRenderer として構築 |
| BlendShape delta vertices | Implemented | Maya上でtarget差分をサンプリングし Unity blendshape frame に変換 |
| Current BlendShape weight | Implemented | Maya current weight を SkinnedMeshRenderer.SetBlendShapeWeight に反映 |
| Constraint metadata | Implemented | constraint node と bake status を MayaSceneData に記録 |
| Import report | Implemented | source evidence / coverage / unsupported / log をMarkdown出力 |
| Validation menu | Implemented | `.ma` / `.mb` / exporter `.json` sample をEditor menuから検証 |

---

## Unity-only `.ma/.mb` preservation path

MAYAtoUnity には、Maya API を使わず Unity 側だけで `.ma/.mb` を解析・保持する経路もあります。

### `.ma` path

```text
.ma file
  ↓
MayaAsciiParser
  ↓
createNode / setAttr / connectAttr / parent / currentUnit ...
  ↓
MayaSceneData
  ↓
UnitySceneBuilder
```

`.ma` path では、Maya ASCII command を実行せず、構造化データとして保持します。

保持対象の例:

- node name / node type
- parent hierarchy
- UUID
- raw attributes
- typed parsed attributes
- plug connections
- raw command statements
- unit information

### `.mb` path

`.mb` は Maya Binary 形式のため、完全互換 parser ではなく preservation-first / best-effort recovery として扱います。

```text
.mb bytes
  ↓
Raw binary preservation + SHA-256
  ↓
IFF-like chunk index
  ↓
Embedded ASCII extraction
  ↓
Null-terminated ASCII reconstruction
  ↓
String table / DAG-like path hints
  ↓
Deterministic node enumeration
  ↓
Chunk placeholder fallback
```

`.mb` recovery では、復元根拠を `MayaNodeProvenance` として記録します。

| Provenance | Meaning |
|---|---|
| `AsciiCommands` | `.ma` command / exporter JSON由来 |
| `MbEmbeddedAscii` | `.mb` 内 command-like text 由来 |
| `MbNullTerminatedAscii` | null-terminated string から復元 |
| `MbDeterministicStringTable` | string table 由来の決定的列挙 |
| `MbChunkPlaceholder` | chunk index 由来のplaceholder |
| `MbHeuristic` | heuristic reconstruction |

---

## Key implementation files

```text
Assets/MayaImporter/
  MayaImporter.cs
  MayaImportOptions.cs
  MayaImportReport.cs
  MayaUnityJsonImporter.cs
  MayaUnityJsonModels.cs
  MayaUnityJsonMeshBuilder.cs
  MayaUnityJsonRuntimeBuilder.cs
  MayaUnityJsonSceneConverter.cs

Assets/MayaImporter/Editor/
  MayaImportValidationMenu.cs

Tools/MayaExporter/
  maya_to_unity_exporter.py

Samples/
  SimpleHierarchy.ma
  MaterialTexture.ma
  CameraLight.ma
  TransformAnimation.ma
  ConstraintSample.ma
  ExporterJson/SimpleMeshMaterialAnimation.json

Docs/
  SupportedNodes.md
  MayaExporterPlan.md
  ValidationWorkflow.md
```

---

## How to validate

Unity Editor menu:

```text
Tools/MAYAtoUnity/Validate All Samples
```

or select an exporter JSON file such as:

```text
Samples/ExporterJson/SimpleMeshMaterialAnimation.json
```

then run:

```text
Tools/MAYAtoUnity/Validate Selected Exporter JSON
```

The validation path imports the file, builds Unity objects, writes an import report, and logs node / connection counts.

---

## Example: programmatic import

```csharp
using MayaImporter.Core;
using UnityEngine;

public class MayaImportExample : MonoBehaviour
{
    public string filePath;

    private void Start()
    {
        var options = new MayaImportOptions();
        options.GenerateImportReport = true;

        MayaSceneData scene;
        MayaImportLog log;

        GameObject root = MayaImporter.ImportIntoScene(
            filePath,
            options,
            out scene,
            out log
        );

        Debug.Log("Imported root: " + root.name);
        Debug.Log("Node count: " + scene.Nodes.Count);
        Debug.Log("Connection count: " + scene.Connections.Count);
    }
}
```

For exporter JSON:

```csharp
using MayaImporter.Core;
using UnityEngine;

public class MayaJsonImportExample : MonoBehaviour
{
    public string jsonPath;

    private void Start()
    {
        var options = new MayaImportOptions();

        MayaSceneData scene;
        MayaImportLog log;

        GameObject root = MayaUnityJsonImporter.ImportJsonIntoScene(
            jsonPath,
            options,
            out scene,
            out log
        );

        Debug.Log("Imported JSON root: " + root.name);
    }
}
```

---

## Design policy

### Preservation first

未対応ノードや完全変換できない情報を捨てず、MayaSceneData / NodeRecord / RawAttributeValue / ConnectionRecord として保持します。

### Deterministic reconstruction

同じ入力に対してなるべく同じ出力になるよう、node order、reconstruction stage、fallback を明示しています。

### Baked behavior where appropriate

Constraint のような Maya runtime solver を Unity 側で完全再実装するのではなく、Maya 側で評価した結果を AnimationCurve として bake します。

### Honest limitations

このツールは Unity 公式 FBX workflow や Maya 本体の完全代替ではありません。  
特に `.mb` の完全解読、Arnold / plugin shader network、全deformer、全constraint solver、全animation tangentの完全互換は保証しません。

---

## Current limitations

- `.mb` は完全互換 parser ではなく best-effort recovery
- Material / Shader の完全見た目一致は未保証
- Complex rig / deformer / reference / namespace は追加検証が必要
- BlendShape は deltaVertices 中心で、deltaNormals / deltaTangents は今後強化予定
- Constraint は solver再実装ではなく Transform animation bake
- Animation tangent / interpolation の完全再現は今後対応予定
- Unity実機compile / runtime validation は継続的に拡張予定

---

## Roadmap

### Short term

- Unity compile verification
- More exporter JSON golden samples
- README screenshots / GIF
- Import report sample追加
- Animation tangent / interpolation metadata対応

### Mid term

- Namespace / reference support
- Texture copy/import pipeline
- More rig / skin validation samples
- Automated editor tests
- CI regression checks

### Long term

- Batch import UI
- Project-wide DCC validation
- AssetUtility との連携
- DCC pipeline toolset として統合

---

## Portfolio wording

採用資料では、以下のように説明できます。

> Maya / DCC データを Unity に取り込むための Importer / Pipeline Tool を開発しました。Maya側Exporterから出力したJSONをUnityで読み込み、Hierarchy、Mesh topology、SubMesh、Material、Texture、Camera、Light、Animation、Skin、BindPose、BlendShape、Constraint bakeを再構築します。加えて、`.ma/.mb` のUnity-only preservation pathも実装し、復元根拠を Import Report と SceneData に残すことで、Technical Artist / Tools Programmer 向けの検証可能なDCC連携ツールとして設計しました。

避けるべき表現:

```text
Maya完全互換
Unity公式FBXの代替
あらゆるMayaシーンを完全再現
Arnold / plugin shader完全対応
全rig / all deformer完全再現
```

推奨表現:

```text
Maya / DCC scene data bridge
Preservation-first Unity importer
Exporter JSON based deterministic reconstruction
Validation-oriented DCC pipeline tool
Constraint baking / Skin / BlendShape aware importer
```
