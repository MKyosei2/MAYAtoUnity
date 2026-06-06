============================================================
MAYAtoUnity
============================================================

■ 作品概要
MAYAtoUnity は、Maya のシーンデータを Unity に取り込み、Unity 上で階層・メッシュ・マテリアル・カメラ・ライト・アニメーション・スキン・ブレンドシェイプ等を再構築し、同時にインポート時のログやレポートを出力する DCC パイプラインツールです。
単に「インポートできる」ことではなく、DCC からゲームエンジンへデータを移す際に、何が再現され、何が近似され、何が未対応なのかを確認できるようにすることを目的にしています。

■ 実行環境等を含めた実行方法
リポジトリ：MKyosei2/MAYAtoUnity
実行環境：Unity Editor 6000.3.0f1
補足：ProjectSettings/ProjectVersion.txt 上の Unity バージョンは 6000.3.0f1 です。

実行手順：
1. Unity Hub から MAYAtoUnity のプロジェクトフォルダを開きます。
2. Unity の script compile が完了するまで待ちます。
3. 上部メニューから以下を実行します。
   Tools > MAYAtoUnity > Advanced > Validate All Samples
4. Samples フォルダ内の .ma / .mb / .json が検証され、Unity シーン上に import 結果が生成されます。
5. 生成されたレポートを確認します。
   Assets/MayaImported/Reports
6. JSON Bridge の動作を個別に確認する場合は、以下のファイルを Project view で選択します。
   Samples/ExporterJson/SimpleMeshMaterialAnimation.json
7. 選択した状態で以下を実行します。
   Tools > MAYAtoUnity > Advanced > Validate Selected Exporter JSON
8. Hierarchy、Console、Import Report を確認します。

確認時に見るポイント：
- Transform hierarchy が再構築されているか。
- Mesh / SubMesh / Material が割り当てられているか。
- Camera / Light が生成されているか。
- AnimationClip、BlendShape、Constraint metadata が report に記録されているか。
- Unsupported feature や warning が隠されず report に残っているか。

■ プログラムを作成する上で苦労した箇所
1. Maya と Unity のデータ表現の違いを吸収する部分
   Maya の DAG path、node、attribute、connection、animation curve、skinCluster、blendShape と、Unity の GameObject、Transform、Mesh、Renderer、AnimationClip の対応関係を整理する必要がありました。

2. 完全再現できない情報をどう扱うか
   Maya の constraint、simulation、.mb binary 情報などは Unity 上でそのまま完全再現することが難しいため、黙って捨てるのではなく、fallback、unsupported feature、provenance として report に残す方針にしました。

3. JSON Bridge と .ma/.mb preservation path の両立
   Maya exporter JSON から再構築する path と、Unity だけで .ma/.mb 情報を保存・復元する path の両方を扱うため、内部データを MayaSceneData に寄せて、あとから検証しやすい構造にする点に苦労しました。

4. 検証しやすいポートフォリオにすること
   採用担当者やレビュワーが短時間で確認できるように、Validate All Samples、Import Report、ExpectedReportStats、ValidationWorkflow を用意し、実行結果を追跡できるようにしました。

■ 力を入れて作った部分 / プログラム上で特に注意して見てもらいたい箇所
1. JSON Bridge の entry point と profiling
   ファイル：Assets/MayaImporter/MayaUnityJsonImporter.cs
   見てほしい点：
   - JSON read / parse / schema validation / scene conversion / hierarchy build / runtime attachment / report write を stage ごとに分けています。
   - 各 stage の timing や log を残し、失敗時にも原因を追いやすくしています。

2. Unity 側への runtime attachment
   ファイル：Assets/MayaImporter/MayaUnityJsonRuntimeBuilder.cs
   見てほしい点：
   - Material、Texture、Camera、Light、Animation、SkinnedMeshRenderer、BlendShape の割り当てを担当しています。
   - Maya 側のデータを Unity Component に変換する中心部分です。

3. Mesh 構築と SubMesh / Material assignment
   ファイル：Assets/MayaImporter/MayaUnityJsonMeshBuilder.cs
   見てほしい点：
   - vertices、normals、UV、indices、submesh を Unity Mesh に変換します。
   - face material assignment を Unity の submesh/material slot に対応させる部分を確認してほしいです。

4. Import report 生成
   ファイル：Assets/MayaImporter/MayaImportReport.cs
   見てほしい点：
   - source evidence、node count、connection count、unsupported feature、warning、validation checklist を Markdown report として残します。
   - ツールの主張である traceability を支える部分です。

5. Validation menu
   ファイル：Assets/MayaImporter/Editor/MayaImportValidationMenu.cs
   見てほしい点：
   - サンプル検証をメニューから実行できるようにし、短時間で挙動を確認できるようにしています。

■ 参考にしたソースファイルについて
外部の特定ソースファイルをコピーして実装したものはありません。
参考にした考え方は、Unity Editor scripting、Unity Mesh / Renderer / AnimationClip の API、Maya の node / attribute / connection / animation curve の概念、DCC pipeline における import report / validation workflow の考え方です。

作品内で実装意図を確認しやすいファイル：
- README.md
- Docs/ValidationWorkflow.md
- Docs/SupportedNodes.md
- Docs/MayaExporterPlan.md
- Tools/MayaExporter/maya_to_unity_exporter.py
- Assets/MayaImporter/MayaUnityJsonImporter.cs
- Assets/MayaImporter/MayaUnityJsonRuntimeBuilder.cs
- Assets/MayaImporter/MayaUnityJsonMeshBuilder.cs
- Assets/MayaImporter/MayaImportReport.cs


============================================================