# MAYAtoUnity

**MAYAtoUnity** は、Maya の `.ma` / `.mb` シーンファイルを Unity 側で解析し、Unity の GameObject 階層・コンポーネント・検証可能な中間データへ再構築するための Importer / DCC Pipeline Tool です。

このプロジェクトの主眼は、単に「Maya ファイルを読み込む」ことではありません。  
Maya のノード、属性、接続、階層、復元経路を Unity 上で追跡可能な形に変換し、ゲーム開発における **DCC → Unity 連携、アセット検証、インポート自動化、Technical Artist 向けツール開発** の土台を作ることを目的としています。

> 現在の実装は研究開発・ポートフォリオ向けのカスタム Importer です。  
> Autodesk Maya / Maya API / FBX SDK には依存せず、Unity 側だけで解析・復元・検証できる構成を目指しています。

---

## 1. このツールが解決しようとしている課題

ゲーム制作では、Maya などの DCC ツールで作成したデータを Unity に持ち込む際、通常は FBX や専用エクスポータを利用します。  
しかし、実際の制作現場では以下のような問題が起こります。

- Maya 側のノード構造や接続情報が Unity 側で見えなくなる
- Import 後に「なぜこの結果になったのか」を追跡しにくい
- FBX 変換時に失われる属性や補助情報がある
- Maya が入っていない環境では検証・自動化が難しい
- 大量の DCC データを Unity 側で機械的に検査したい
- Tool / Technical Artist が Maya データの中身を Unity 側で確認したい

MAYAtoUnity は、こうした問題に対して **「変換結果だけでなく、変換根拠も保持する」** ことを重視した Importer です。

---

## 2. コンセプト

### Preservation-first importer

MAYAtoUnity は、完全に再現できない情報を無理に捨てたり、曖昧に変換したりするのではなく、まず元データを可能な限り保持します。

- Maya ノード名
- Maya ノードタイプ
- 親子関係
- UUID
- Raw attributes
- Raw connection plugs
- `.mb` 解析時の復元経路
- Raw binary / raw ascii source evidence

これにより、未対応ノードであっても Unity Inspector 上で「何が存在していたか」を確認できます。

### Deterministic reconstruction

Importer の出力は、同じ入力に対してなるべく同じ順序・同じ構造になるように設計しています。

- ノード名による決定的ソート
- ステージ別の `ApplyToUnity`
- NodeType ごとの優先順位付け
- `.mb` 復元経路の provenance 管理

これは、差分確認、テスト、デバッグ、ポートフォリオとしての説明性を高めるための設計です。

### Unity-only pipeline

このリポジトリの大きな制約・特徴は、Maya API に依存しないことです。

- Maya を起動しない
- Autodesk API を呼ばない
- Unity C# 側でパース・データ化・再構築する

そのため、完全な Maya 互換 Importer ではありませんが、Unity 側で DCC データを解析する Tool Engineering の題材として成立する構成になっています。

---

## 3. 全体アーキテクチャ

```text
.ma / .mb file
  ↓
MayaImporter.Parse
  ↓
+----------------------+----------------------+
| .ma path             | .mb path             |
| MayaAsciiParser      | MayaBinaryParser     |
+----------------------+----------------------+
  ↓
MayaSceneData
  ↓
NodeRecord / ConnectionRecord / RawAttributeValue
  ↓
UnitySceneBuilder
  ↓
GameObject hierarchy
  ↓
MayaNodeComponentBase-derived components
  ↓
RuntimeAutoFix / RuntimeAutoBind
```

---

## 4. Main entry point

メインの入口は `MayaImporter` です。

```csharp
using MayaImporter.Core;
using UnityEngine;

public class MayaImportExample : MonoBehaviour
{
    public string mayaFilePath;

    private void Start()
    {
        var options = new MayaImportOptions();

        MayaSceneData scene;
        MayaImportLog log;

        GameObject root = MayaImporter.ImportIntoScene(
            mayaFilePath,
            options,
            out scene,
            out log
        );

        Debug.Log($"Imported root: {root.name}");
        Debug.Log($"Node count: {scene.Nodes.Count}");
        Debug.Log($"Connection count: {scene.Connections.Count}");
    }
}
```

`ImportIntoScene` は以下を行います。

1. ファイル拡張子を確認する
2. `.ma` なら `MayaAsciiParser` を使う
3. `.mb` なら `MayaBinaryParser` を使う
4. `MayaSceneData` を生成する
5. `UnitySceneBuilder` で Unity Hierarchy を構築する
6. ImportLog を返す

---

## 5. Data model

### MayaSceneData

`MayaSceneData` は Importer の中心となる中間表現です。

主な保持内容:

| Data | Purpose |
|---|---|
| `SourcePath` | 元ファイルパス |
| `SourceKind` | `.ma` / `.mb` / unknown の識別 |
| `RawAsciiText` | `.ma` 由来の元テキスト |
| `RawBinaryBytes` | `.mb` 由来の元バイナリ |
| `RawSha256` | 元データの検証用ハッシュ |
| `SceneUnits` | Maya の unit 情報 |
| `Nodes` | Maya ノード一覧 |
| `Connections` | plug 接続一覧 |
| `RawStatements` | デバッグ用 raw command |
| `MbIndex` | `.mb` の chunk / string table 解析補助 |
| `MbStringTable` | `.mb` から抽出した文字列情報 |
| `MbMeshHints` | `.mb` 由来の mesh hint |

### NodeRecord

`NodeRecord` は Maya ノード 1 つを表すデータです。

| Field | Meaning |
|---|---|
| `Name` | Maya node name |
| `NodeType` | Maya node type |
| `ParentName` | Parent node name |
| `Uuid` | Maya UUID |
| `Provenance` | どの経路で復元されたか |
| `Attributes` | setAttr などで取得した属性 |
| `SetAttrStatements` | デバッグ用 setAttr statement |

### ConnectionRecord

`ConnectionRecord` は Maya の `connectAttr` に相当する接続を保持します。

| Field | Meaning |
|---|---|
| `SrcPlug` | 接続元 plug |
| `DstPlug` | 接続先 plug |
| `Force` | force 接続かどうか |

### RawAttributeValue

Maya の属性値は、まず raw token として保存し、可能な場合だけ型付き値へ変換します。

対応している typed value の例:

- bool
- int
- float
- Vector2
- Vector3
- Vector4
- Matrix4x4
- int array
- float array
- string array

---

## 6. `.ma` parsing

`.ma` は Maya ASCII の command 形式を解析します。

主な対応 command:

| Command | Purpose |
|---|---|
| `createNode` | NodeRecord の生成 |
| `setAttr` | RawAttributeValue の記録 |
| `connectAttr` | ConnectionRecord の生成 |
| `parent` | 親子関係の記録 |
| `currentUnit` | Scene unit の記録 |
| `rename` | Node 名変更への対応 |
| `fileInfo` | File metadata の記録 |
| `requires` | Maya plugin / requirement 情報 |
| `workspace` | Workspace rule 情報 |
| `namespace` | Namespace operation 情報 |
| `addAttr` | Dynamic attribute 情報 |
| `deleteAttr` | Attribute 削除情報 |
| `lockNode` | Node lock 情報 |
| `select` | Selection command 記録 |
| `setKeyframe` | Animation command 記録 |
| `setDrivenKeyframe` | Driven key command 記録 |
| `animLayer` | Animation layer command 記録 |
| `expression` | Expression command 記録 |
| `scriptNode` | Script node command 記録 |

### `.ma` parser の設計意図

- Maya command を完全実行するのではなく、Unity 側で構造化データへ変換する
- 解析できる値は型付きにする
- 解析できない値も raw token として残す
- Import 後に Inspector / DebugLog / Report で確認できるようにする

---

## 7. `.mb` binary recovery

`.mb` は Maya Binary 形式であり、`.ma` のように単純な command text として読めません。  
MAYAtoUnity では、`.mb` を完全再現するのではなく、**復元可能な証拠を段階的に拾う best-effort pipeline** として扱います。

### `.mb` pipeline

```text
Raw .mb bytes
  ↓
SetRawBinary + SHA-256
  ↓
IFF-like chunk index
  ↓
String table decode
  ↓
Deterministic node enumeration
  ↓
Embedded ASCII extraction
  ↓
Null-terminated ASCII reconstruction
  ↓
Shading / texture hint tagging
  ↓
Mesh topology hint extraction
  ↓
Heuristic graph rebuild
  ↓
Structured rebuild / TRS rebuild / DAG post process
  ↓
Chunk placeholder fallback
```

### Provenance tracking

`.mb` から復元したノードは、復元経路を `MayaNodeProvenance` として保持します。

| Provenance | Meaning |
|---|---|
| `AsciiCommands` | `.ma` command 由来 |
| `MbEmbeddedAscii` | `.mb` 内の command-like text 由来 |
| `MbNullTerminatedAscii` | null-terminated string から復元 |
| `MbDeterministicStringTable` | string table から決定的に列挙 |
| `MbChunkPlaceholder` | chunk index 由来の placeholder |
| `MbHeuristic` | heuristic reconstruction |

これにより、復元できたノードについて「確実に読めたもの」と「推定で作ったもの」を区別できます。

### `.mb` support policy

この Importer は `.mb` を完全互換で読み込むことを目的としていません。  
代わりに、以下を重視しています。

- 元バイナリを保持する
- 復元根拠を明示する
- 失敗時も空の結果ではなく inspectable な placeholder を残す
- 復元経路をログ・データに残す
- 将来の decoder 拡張に耐える中間構造を作る

---

## 8. Unity reconstruction

`UnitySceneBuilder` は `MayaSceneData` から Unity Hierarchy を構築します。

### Build steps

1. `scene.Nodes` を deterministic order で並べる
2. 各 `NodeRecord` に対応する GameObject を生成する
3. `NodeFactory` 経由で node type に対応する Component を付与する
4. `MayaNodeComponentBase.InitializeFromRecord` で raw attributes / connections を注入する
5. ParentName に基づいて Transform hierarchy を復元する
6. Stage priority に基づいて `ApplyToUnity` を呼ぶ
7. Material post process を実行する
8. Runtime settings / optional player component を付与する

### Stage priority

ノードの適用順序は、Unity 側の依存関係を考慮して段階分けしています。

| Priority | Node examples |
|---:|---|
| 0 | transform, joint |
| 10 | camera, light |
| 20 | mesh, nurbsCurve, nurbsSurface |
| 30 | blendShape, skinCluster |
| 40 | constraint, IK, motionPath |
| 50 | shader / texture-like nodes |
| 60 | shadingEngine |
| 70 | animCurve / animation nodes |
| 800 | other nodes |

この設計により、Transform や Mesh などの基礎構造を先に構築し、その後に deformer / constraint / material / animation を適用できます。

---

## 9. Node components

各 Maya node は Unity 上では `MayaNodeComponentBase` 派生 Component として表現されます。

共通で保持する情報:

- `NodeName`
- `NodeType`
- `ParentName`
- `Uuid`
- `Attributes`
- `Connections`

### Generic node support

未対応 node type についても、完全に捨てるのではなく Generic / Unknown component として保持します。

Generic node では:

- 属性数
- 接続数
- 型付き属性 summary
- Inspector 向け preview

を表示できるようにし、将来的な node-specific 実装に繋げられる構成にしています。

---

## 10. RuntimeAutoFix / RuntimeAutoBind

Import 直後だけでなく、Prefab として保存したあとに scene load された場合も壊れにくくするため、runtime 補助処理を用意しています。

### RuntimeAutoFix

目的:

- Missing component の補完
- Constraint 系 component の補助追加
- BlendShape / SkinCluster 周辺の接続補助
- Parent consistency の補正

### RuntimeAutoBind

目的:

- Constraint target の再バインド
- Deformer / mesh 的な接続の補助
- BlendShape が存在する mesh の検出

これらは完全な Maya runtime simulation ではなく、imported prefab を Unity 上で扱いやすくするための repair layer です。

---

## 11. Import options

`MayaImportOptions` で挙動を切り替えます。

| Option | Purpose |
|---|---|
| `KeepRawStatements` | raw command を保持する |
| `RawStatementsMaxEntries` | raw command 保持数の上限 |
| `SetAttrStatementsMaxPerNode` | node ごとの setAttr 保存上限 |
| `CreateRootGameObject` | root GameObject を作成する |
| `Conversion` | Maya → Unity 座標変換方式 |
| `CreateUnityComponents` | Unity Component の生成を有効化 |
| `SaveAssets` | Editor 側で asset 保存を行う |
| `OutputFolder` | 保存先 folder |
| `SaveMeshes` | mesh asset を保存する |
| `SaveMaterials` | material asset を保存する |
| `SaveTextures` | texture asset を保存する |
| `SaveAnimationClip` | AnimationClip 保存 path を使う |
| `SavePrefab` | prefab として保存する |
| `KeepImportedRootInScene` | import 後の scene instance を残す |
| `AttachDecodedAttributeSummary` | Inspector 向け summary を付与する |
| `MbTryExtractEmbeddedAscii` | `.mb` 内 command-like text を抽出する |
| `MbTryExtractNullTerminatedAscii` | null-terminated string から command を復元する |
| `MbDeterministicEnumerateNodes` | `.mb` string table から node を列挙する |
| `MbCreateChunkPlaceholderNodes` | 最後の fallback として chunk placeholder を作る |

---

## 12. Current implementation status

| Area | Status | Notes |
|---|---:|---|
| `.ma` command parsing | Implemented | 主要 command を構造化データとして保持 |
| `.mb` raw preservation | Implemented | Raw bytes と SHA-256 を保持 |
| `.mb` embedded text recovery | Implemented / best-effort | confidence score と statement 数で判断 |
| `.mb` string table hints | Implemented / best-effort | node enumeration や texture hint に利用 |
| Node identity storage | Implemented | name/type/parent/uuid を保持 |
| Raw attributes | Implemented | token と typed parsed value を保持 |
| Connections | Implemented | src/dst plug を保持 |
| Unity hierarchy build | Implemented | deterministic order で GameObject 化 |
| Generic node fallback | Implemented | 未対応 node も inspectable に保持 |
| Material reconstruction | Partial | Maya shader 完全互換ではない |
| Mesh reconstruction | Partial | source data と対応 node に依存 |
| Rig / skin / deformer | Partial | runtime repair と node storage 中心 |
| Animation | Experimental | command record / optional clip path は存在 |
| Production validation | In progress | sample / test / benchmark の追加が必要 |

---

## 13. Limitations

このプロジェクトは、Maya や Unity の公式 FBX workflow を置き換えるものではありません。

現在の主な制限:

- `.mb` は完全互換 parser ではなく best-effort recovery
- Maya の全 node type を Unity 上で完全再現するわけではない
- Material / Shader の見た目完全一致は保証しない
- Deformer / Rig / Constraint / Animation の完全な Maya runtime 再現は未対応
- 実運用には多数の real asset による validation が必要
- Editor UI、import report、automated test はさらに整備が必要

ただし、これらの制限を隠さず、raw data / provenance / generic node として残す設計にしています。

---

## 14. Verification plan

今後、README や portfolio 上で説得力を高めるために、以下の検証を追加予定です。

### Import report

Import 後に以下を出力する report を追加する。

- source file path
- source hash
- node count
- connection count
- node type breakdown
- `.mb` provenance breakdown
- unsupported node list
- warning / error count

### Golden sample tests

小さな `.ma` sample を用意し、以下を自動検証する。

- createNode 数
- parent hierarchy
- setAttr parse 結果
- connection count
- unit 情報
- transform attribute

### Visual comparison

Maya 側 screenshot と Unity import result を並べて掲載する。

- hierarchy comparison
- material comparison
- mesh / object count comparison
- import log screenshot

---

## 15. Recommended repository additions

ポートフォリオとしてさらに強く見せるため、以下の追加を推奨します。

```text
Docs/
  Architecture.md
  MbRecoveryPipeline.md
  SupportedNodes.md
  ImportReportSample.md

Samples/
  SimpleHierarchy.ma
  MaterialSample.ma
  ConstraintSample.ma
  BinaryRecoverySample.mb

Screenshots/
  maya_hierarchy.png
  unity_import_result.png
  inspector_node_summary.png

Tests/
  ParserTests.cs
  SceneBuilderTests.cs
  MbRecoveryTests.cs
```

---

## 16. Roadmap

### Short term

- README に screenshot / GIF を追加
- sample `.ma` を追加
- import report 出力を追加
- 対応 node type 一覧を追加
- parser unit test を追加

### Mid term

- Unity Editor import window を追加
- `.ma` parser の test coverage を拡大
- material reconstruction の精度改善
- mesh / transform / connection の validation tool を追加
- unsupported node report を整備

### Long term

- project-wide batch import
- CI 上で parser regression test
- AssetUtility との連携
- import result の optimization / validation pipeline 化
- DCC pipeline toolset として統合

---

## 17. Portfolio / technical appeal points

このプロジェクトで示せる技術要素:

- Unity Editor / Runtime tooling
- DCC pipeline engineering
- Parser implementation
- Scene graph reconstruction
- Intermediate data model design
- Raw data preservation
- Deterministic import pipeline
- Best-effort binary recovery
- Fallback / graceful degradation design
- Inspector-friendly debugging
- Technical Artist / Tools Programmer 向けの問題解決力

---

## 18. How to present this project

書類・ポートフォリオでは、以下のように説明できます。

> Maya の `.ma/.mb` ファイルを Unity 側で解析し、Maya API に依存せずにノード・属性・接続・階層を構造化データとして保持し、GameObject 階層へ再構築する Importer を開発しました。  
> `.mb` については raw binary 保持、embedded ASCII 抽出、null-terminated string 復元、string table 由来の決定的ノード列挙など複数の recovery path を実装し、復元経路を provenance として追跡可能にしました。  
> 完全再現できない node も generic component として保持し、Inspector 上で属性・接続・復元情報を確認できる preservation-first pipeline として設計しています。

---

## 19. Related project

この Importer は、別リポジトリの `AssetUtility` と組み合わせることで、以下のような DCC-to-Unity pipeline として見せることができます。

```text
Maya scene import
  ↓
Unity hierarchy reconstruction
  ↓
Asset scan
  ↓
Texture / material / polygon inspection
  ↓
Optimization
  ↓
Prefab / scene validation
```

---

## 20. Disclaimer

This is an independent custom importer project for technical research and portfolio purposes.  
It is not affiliated with Autodesk, Unity Technologies, or the official Maya / Unity FBX pipeline.
