# MAYAtoUnity Import Profile

Source: `C:\Users\kyose\ドキュメント\GitHub\MAYAtoUnity\Samples\ExporterJson\SimpleMeshMaterialGolden.json`
Success: `True`

| Stage | ms | Warnings | Errors | Note |
|---|---:|---:|---:|---|
| json_read | 0.768 | 0 | 0 | bytes=2058 |
| json_parse | 0.081 | 0 | 0 | schemaVersion=10 |
| schema_validation | 0.001 | 0 | 0 | warnings=0 errors=0 |
| scene_conversion | 0.065 | 0 | 0 | nodes=3 |
| hierarchy_build | 0.434 | 1 | 0 | SimpleMeshMaterialGolden |
| json_reload_for_runtime_attachment | 0.533 | 0 | 0 | ok |
| context_build | 0.019 | 0 | 0 | transformAliases=8 |
| material_build | 0.112 | 0 | 0 | materials=1 |
| mesh_skin_blendshape_attach | 0.247 | 0 | 0 | Transform lookups=1 hits=1 misses=0 | Component lookups=2 hits=0 misses=2 |
| material_assign | 0.005 | 0 | 0 | Transform lookups=2 hits=2 misses=0 | Component lookups=2 hits=0 misses=2 |
| camera_attach | 0.003 | 0 | 0 |  |
| light_attach | 0.001 | 0 | 0 |  |
| animation_attach | 0 | 0 | 0 |  |
| report_write | 38.371 | 0 | 0 |  |

## Cache statistics

Transform lookups=2 hits=2 misses=0 | Component lookups=2 hits=0 misses=2
