# MAYAtoUnity

**MAYAtoUnity** は、Maya のシーンデータを Unity 内に取り込み、保存・検証・プロファイル・再構築するための **Unity Editor / DCC パイプラインツール** です。

このリポジトリは、**Technical Artist / Tools Programmer 向けのポートフォリオ作品** として設計しています。解決したい制作上の課題は、DCC ツール上のシーン情報をゲームエンジンへ移すときに、何が取り込まれ、何が近似され、何が未対応なのかを追跡できる状態にすることです。

> スコープ注記: このツールは Autodesk Maya、FBX、Unity 公式インポートパイプラインの代替ではありません。主張できる範囲は、Maya / DCC データを Unity で検証可能にする、決定的で監査しやすいブリッジツールです。

---

## ポートフォリオ要約

```text
Maya scene data
  -> Maya側 JSON exporter または Unity側 .ma/.mb 保存パス
  -> 中間データ MayaSceneData
  -> 決定的な Unity hierarchy 再構築
  -> mesh / material / texture / camera / light / animation / skin / blendshape 付与
  -> validation log + import report + profile report
```

このツールの価値は、単に「インポートできる」ことではありません。重要なのは **traceability / 追跡可能性** です。source node、attribute、fallback、unsupported feature、warning、生成された Unity object、stage timing を記録し、レビュー時に「何が起きたか」を確認できるようにしています。

---

## 課題 / 利用者 / 出力

| 項目 | 内容 |
|---|---|
| 課題 | DCC からゲームエンジンへの移行では、情報が静かに失われることがあり、原因調査が難しくなります。 |
| 主な利用者 | Technical Artist、Tools Programmer、Pipeline Engineer、Unity Developer。 |
| 入力 | Maya exporter JSON、Maya ASCII `.ma`、best-effort の Maya Binary `.mb`。 |
| 出力 | Unity hierarchy、mesh、renderer、material、camera、light、animation clip、skin / blendshape data、report。 |
| 安全性の方針 | 未対応データを黙って破棄せず、証拠として保存・レポートします。 |

---

## 30秒概要

MAYAtoUnity には 2 つの import path があります。

```text
Path A: Exporter JSON Bridge
Maya scene
  -> Tools/MayaExporter/maya_to_unity_exporter.py
  -> exporter JSON
  -> MayaUnityJsonImporter
  -> Unity hierarchy / mesh / material / animation / skin / blendshape / constraint metadata
  -> import report + validation log + profile report

Path B: Unity-only .ma/.mb preservation
.ma / .mb file
  -> parser / recovery layer
  -> MayaSceneData
  -> UnitySceneBuilder
  -> preserved node records, attributes, connections, provenance, fallback records, and report
```

---

## レビュー手順

短時間で確認する場合は、以下の順番を推奨します。

```text
1. この README を読む。
2. Unity project を開く。
3. Tools/MAYAtoUnity/Validate All Samples を実行する。
4. 生成された import report と log を確認する。
5. Samples/ExporterJson/SimpleMeshMaterialAnimation.json を開く。
6. Assets/MayaImporter/MayaUnityJsonImporter.cs を確認する。
7. Assets/MayaImporter/MayaUnityJsonRuntimeBuilder.cs を確認する。
8. Docs/ValidationWorkflow.md を確認する。
```

Unity メニューの想定 entry point:

```text
Tools/MAYAtoUnity/Validate All Samples
Tools/MAYAtoUnity/Validate Selected Exporter JSON
```

---

## デモ用サンプル

### 標準サンプル

```text
Samples/SimpleHierarchy.ma
Samples/MaterialTexture.ma
Samples/CameraLight.ma
Samples/TransformAnimation.ma
Samples/ConstraintSample.ma
Samples/ExporterJson/SimpleMeshMaterialAnimation.json
```

### FX / 物理デモサンプル

```text
Samples/FxPhysicsShowcase.ma
Docs/Samples/FxPhysicsShowcase.md
Tools/MayaSamples/create_fx_physics_showcase_scene.py
```

`FxPhysicsShowcase.ma` は、3D モデル階層、particle、gravity / turbulence field、rigidBody、nucleus / nCloth / nRigid / dynamicConstraint、camera、light、material、setKeyframe を含むデモ用 `.ma` です。

このサンプルの目的は、Unity 上で Maya の particle / rigid body / nCloth を完全再現することではありません。目的は、**FX / 物理演算に関する source node、attribute、connection、keyframe、unsupported simulation metadata を保存・レポートできることを示す**ことです。

Maya 上で見栄えのある生成版を作る場合は、以下を Maya の Python 環境で実行します。

```python
import sys
sys.path.append(r"<repo>/Tools/MayaSamples")
import create_fx_physics_showcase_scene as demo

demo.build_scene(r"<repo>/Samples/FxPhysicsShowcase_Generated.ma")
```

---

## JSON Bridge の実装済み範囲

| 領域 | 状態 | 内容 |
|---|---:|---|
| Transform hierarchy | 実装済み | Maya path / parent path から Unity hierarchy を構築します。 |
| Mesh topology | 実装済み | vertices、normals、UV、triangle indices を再構築します。 |
| SubMesh assignment | 実装済み | face material assignment を Unity submesh に変換します。 |
| Material color | 実装済み | lambert / phong / blinn / standardSurface 風の base color mapping に対応します。 |
| Texture binding | 実装済み | file texture path を可能な範囲で `_BaseMap` / `_MainTex` に割り当てます。 |
| Camera | 実装済み | focal length と clipping plane metadata を保持します。 |
| Light | 実装済み | directional / point / spot / area fallback を扱います。 |
| Animation curves | 実装済み | translate / rotate / scale の animCurve data を Unity AnimationClip curve に変換します。 |
| Constraint metadata | 実装済み | constraint node と bake status を scene data に保存します。 |
| Constraint baking policy | 実装済み | Maya solver を Unity で再実装せず、baked transform animation を優先します。 |
| Joints | 実装済み | joint hierarchy / jointOrient / matrix metadata を保持します。 |
| Skin weights | 実装済み | exported vertex ごとに top 4 weights を Unity BoneWeight に変換します。 |
| Bindposes | 実装済み | skinCluster bindPreMatrix を優先し、なければ joint inverse matrix を fallback として使います。 |
| SkinnedMeshRenderer | 実装済み | skin data がある場合に skinned renderer を構築します。 |
| BlendShape deltas | 実装済み | sampled delta vertices を Unity blendshape frame として取り込みます。 |
| BlendShape weight | 実装済み | 現在の Maya weight を SkinnedMeshRenderer に適用します。 |
| Schema validation | 実装済み | scene conversion 前に warning / error を出力します。 |
| Import profiling | 実装済み | import stage timing と cache statistics を記録します。 |
| Import report | 実装済み | source evidence、coverage、unsupported entries、log output を Markdown report に出力します。 |
| Validation menu | 実装済み | sample import と report generation 用の Editor menu を提供します。 |

---

## Unity-only `.ma/.mb` 保存パス

MAYAtoUnity は Maya を呼び出さずに、Maya ASCII / Maya Binary 情報を保存するパスも持っています。

### `.ma` path

```text
.ma file
  -> MayaAsciiParser
  -> createNode / setAttr / connectAttr / parent / currentUnit records
  -> MayaSceneData
  -> UnitySceneBuilder
```

保存対象には、node name、node type、parent hierarchy、UUID、raw attributes、typed parsed attributes、plug connections、raw command statements、unit information などが含まれます。

### `.mb` path

`.mb` は、完全な Maya Binary parser ではなく、best-effort の preservation / recovery として扱います。

```text
.mb bytes
  -> raw binary preservation + SHA-256
  -> IFF-like chunk index
  -> embedded ASCII extraction
  -> null-terminated ASCII reconstruction
  -> string table / DAG-like path hints
  -> deterministic node enumeration
  -> chunk placeholder fallback
```

各 reconstructed record には provenance category を記録し、どの情報源から復元されたかを確認できるようにします。

| Provenance | 意味 |
|---|---|
| `AsciiCommands` | `.ma` command または exporter JSON source。 |
| `MbEmbeddedAscii` | `.mb` 内で見つかった command-like text。 |
| `MbNullTerminatedAscii` | null-terminated string から復元された情報。 |
| `MbDeterministicStringTable` | string table から決定的に列挙した情報。 |
| `MbChunkPlaceholder` | chunk index から作成した placeholder。 |
| `MbHeuristic` | heuristic reconstruction。 |

---

## アーキテクチャ

```text
Assets/MayaImporter/
  MayaImporter.cs                    # public import entry point
  MayaImportOptions.cs               # import configuration
  MayaImportReport.cs                # Markdown report writer
  MayaUnityJsonImporter.cs           # JSON bridge entry point, schema validation, profiling
  MayaUnityJsonModels.cs             # exporter JSON model
  MayaUnityJsonMeshBuilder.cs        # Unity Mesh construction
  MayaUnityJsonRuntimeBuilder.cs     # renderer/material/camera/light/skin/blendshape attachment
  MayaUnityJsonSceneConverter.cs     # exporter JSON -> MayaSceneData

Assets/MayaImporter/Editor/
  MayaImportValidationMenu.cs        # sample validation menu

Tools/MayaExporter/
  maya_to_unity_exporter.py          # Maya-side JSON exporter

Tools/MayaSamples/
  create_fx_physics_showcase_scene.py # Maya visual demo scene generator

Samples/
  SimpleHierarchy.ma
  MaterialTexture.ma
  CameraLight.ma
  TransformAnimation.ma
  ConstraintSample.ma
  FxPhysicsShowcase.ma
  ExporterJson/SimpleMeshMaterialAnimation.json

Docs/
  SupportedNodes.md
  MayaExporterPlan.md
  ValidationWorkflow.md
  Samples/FxPhysicsShowcase.md
```

---

## 検証方法

Unity project を開き、以下を実行します。

```text
Tools/MAYAtoUnity/Validate All Samples
```

または exporter JSON file を選択して、以下を実行します。

```text
Tools/MAYAtoUnity/Validate Selected Exporter JSON
```

推奨サンプル:

```text
Samples/ExporterJson/SimpleMeshMaterialAnimation.json
Samples/FxPhysicsShowcase.ma
```

validation path は、file を import し、Unity object を構築し、import report を書き出し、node / connection counts を log に出力します。

---

## プログラムからの import 例

```csharp
using MayaImporter.Core;
using UnityEngine;

public class MayaJsonImportExample : MonoBehaviour
{
    public string jsonPath;

    private void Start()
    {
        var options = new MayaImportOptions
        {
            GenerateImportReport = true
        };

        MayaSceneData scene;
        MayaImportLog log;

        GameObject root = MayaUnityJsonImporter.ImportJsonIntoScene(
            jsonPath,
            options,
            out scene,
            out log
        );

        Debug.Log("Imported JSON root: " + root.name);
        Debug.Log("Node count: " + scene.Nodes.Count);
        Debug.Log("Connection count: " + scene.Connections.Count);
    }
}
```

---

## 設計方針

### Preservation first

未対応または部分対応のデータを黙って消さないことを重視しています。scene data、raw attributes、connections、provenance、warnings、unsupported feature entries、report output として保持します。

### Deterministic reconstruction

同じ入力からは、可能な限り安定した出力を得られるようにしています。node order、reconstruction stage、fallback behavior、report output を確認しやすい形にします。

### Baked behavior where appropriate

constraint behavior は Maya solver を Unity 上で完全再実装する対象ではありません。ゲームエンジン向けには baked transform animation を取り込み、constraint source metadata を保存する方針です。

### Honest limitations

このプロジェクトでは、完全な Maya 互換を主張しません。DCC bridge / validation tool であり、Maya、FBX、商用 DCC pipeline の完全な代替ではありません。

---

## 現在の制限

- `.mb` support は best-effort preservation / recovery であり、完全な Maya Binary compatibility ではありません。
- Material / shader の見た目の一致は近似です。
- Complex rigs、deformers、references、namespaces、plugin nodes には追加サンプルが必要です。
- BlendShape import は delta vertices が中心で、delta normals / delta tangents は今後の課題です。
- Constraint runtime solving は再実装していません。baked transforms を優先します。
- Animation tangent / interpolation parity は追加検証が必要です。
- Particle、rigid body、nCloth の Unity simulation parity は主張しません。現状は source evidence の保存・レポートが目的です。
- README 用の screenshot / GIF / committed report を増やすと、ポートフォリオとしてさらに確認しやすくなります。

---

## 次の改善

### Short term

- `Docs/Reports/` に sample report を commit する。
- import 前後が分かる README screenshot / GIF を追加する。
- skin、blendshape、animation、constraint 用の golden exporter JSON sample を増やす。
- Unity validation run から実測 import-stage timings を記録する。
- `FxPhysicsShowcase.ma` の validation report を保存する。

### Mid term

- Namespace / reference support。
- Texture copy / import pipeline。
- rig / skin / blendshape validation scene の追加。
- Automated Unity Editor tests。
- 可能であれば CI compile / regression checks。

### Long term

- Batch import UI。
- Project-wide DCC validation。
- AssetUtility との integration。
- Technical Art Pipeline Suite としての統合ポートフォリオ化。

---

## ポートフォリオ用説明文

> Maya / DCC データを JSON として export し、Unity 上で hierarchy、mesh topology、submesh assignment、materials、textures、cameras、lights、animation curves、skin weights、bindposes、blendshapes、constraint metadata を再構築する DCC bridge を開発しました。import report、profiling、fallback handling により、未対応データも黙って消さず、検証可能な証拠として残す preservation-first の pipeline tool として設計しています。

FX / 物理デモを含める場合:

> さらに `.ma` audit path の検証用に、particle、gravity / turbulence field、rigidBody、nucleus / nCloth / nRigid / dynamicConstraint を含む FX / physics showcase sample を作成しました。Unity 上で Maya simulation を完全再現するのではなく、source node、attribute、connection、keyframe、unsupported simulation metadata を保存・レポートする設計にしています。

避けるべき主張:

```text
Maya complete compatibility
Unity official FBX replacement
Perfect reproduction of all Maya scenes
Full Arnold / plugin shader support
Full rig / deformer support
Particle / rigidBody / nCloth simulation parity in Unity
```

使うべき主張:

```text
Maya / DCC scene data bridge
Preservation-first Unity importer
Exporter JSON based deterministic reconstruction
Validation-oriented DCC pipeline tool
Constraint baking / Skin / BlendShape aware importer
Profiled import and report workflow
FX / physics source evidence preservation
```
