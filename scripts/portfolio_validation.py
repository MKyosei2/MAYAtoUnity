#!/usr/bin/env python3
"""Repository-level portfolio validation for MAYAtoUnity.

The validator is intentionally dry-run first. It does not mutate Unity assets.
It checks reviewer-critical files, sample coverage, README honesty, golden exporter
JSON integrity, asmdef health, and writes Markdown/JSON reports so CI failures are
inspectable.

The README check accepts both the older English headings and the newer Japanese
portfolio-review headings.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
import traceback
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Callable, List


@dataclass
class Stage:
    name: str
    success: bool
    milliseconds: float
    warnings: List[str]
    errors: List[str]


@dataclass
class Report:
    tool: str
    dry_run: bool
    success: bool
    stages: List[Stage]
    rollback_plan: List[str]
    generated_samples: List[str]
    limitations_status: str


class ValidationContext:
    def __init__(self, root: Path, report_dir: Path, dry_run: bool) -> None:
        self.root = root
        self.report_dir = report_dir
        self.dry_run = dry_run
        self.stages: List[Stage] = []
        self.rollback_plan: List[str] = []
        self.generated_samples: List[str] = []
        self.limitations_status = "not checked"

    def run_stage(self, name: str, fn: Callable[[], tuple[list[str], list[str]]]) -> None:
        start = time.perf_counter()
        warnings: List[str] = []
        errors: List[str] = []
        try:
            warnings, errors = fn()
        except Exception as exc:  # pragma: no cover - CI diagnostic path
            errors.append(f"Unhandled exception: {exc}")
            errors.append(traceback.format_exc())
        elapsed = (time.perf_counter() - start) * 1000.0
        self.stages.append(Stage(name=name, success=not errors, milliseconds=elapsed, warnings=warnings, errors=errors))

    def has_errors(self) -> bool:
        return any(not s.success for s in self.stages)


def rel(ctx: ValidationContext, path: Path) -> str:
    try:
        return str(path.relative_to(ctx.root)).replace("\\", "/")
    except ValueError:
        return str(path).replace("\\", "/")


def require_paths(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    required = [
        "README.md",
        "Assets/MayaImporter",
        "Assets/MayaImporter/MayaUnityJsonImporter.cs",
        "Assets/MayaImporter/MayaUnityJsonRuntimeBuilder.cs",
        "Assets/MayaImporter/MayaImportProfiler.cs",
        "Assets/MayaImporter/MayaUnityJsonSchemaValidator.cs",
        "Assets/MayaImporter/MayaUnsupportedFeatureRegistry.cs",
        "Assets/MayaImporter/Editor",
        "Packages",
        "ProjectSettings",
        "Tools/MayaExporter",
        "Samples/ExporterJson/SimpleMeshMaterialGolden.json",
        "Samples/Expected/SimpleMeshMaterialGolden.expected.json",
    ]
    optional = [
        "Samples/FxPhysicsShowcase.ma",
        "Docs/Samples/FxPhysicsShowcase.md",
        "Tools/MayaSamples/create_fx_physics_showcase_scene.py",
    ]
    for item in required:
        if not (ctx.root / item).exists():
            errors.append(f"Missing required path: {item}")
    for item in optional:
        if not (ctx.root / item).exists():
            warnings.append(f"Optional portfolio sample path not found yet: {item}")
    return warnings, errors


def check_readme(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    readme = ctx.root / "README.md"
    text = readme.read_text(encoding="utf-8") if readme.exists() else ""
    lower = text.lower()

    required_groups = [
        ("portfolio summary", ["ポートフォリオ要約", "portfolio summary", "30-second overview"]),
        ("reviewer path", ["レビュー手順", "reviewer path"]),
        ("limitations", ["現在の制限", "current limitations"]),
        ("roadmap / next improvements", ["次の改善", "roadmap", "next improvements"]),
        ("portfolio wording", ["ポートフォリオ用説明文", "portfolio wording"]),
        ("honest scope note", ["スコープ注記", "not a full replacement", "代替ではありません"]),
    ]

    for label, alternatives in required_groups:
        if not any(token.lower() in lower for token in alternatives):
            errors.append(f"README is missing reviewer/limitation section: {label}; accepted tokens={alternatives}")

    ctx.limitations_status = "README includes explicit limitations" if not errors else "README limitation coverage incomplete"
    return warnings, errors


def check_samples(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    samples_dir = ctx.root / "Samples"
    ma_files = list(samples_dir.glob("**/*.ma")) if samples_dir.exists() else []
    mb_files = list(samples_dir.glob("**/*.mb")) if samples_dir.exists() else []
    json_files = list(samples_dir.glob("**/*.json")) if samples_dir.exists() else []
    total = len(ma_files) + len(mb_files) + len(json_files)
    if total == 0:
        errors.append("No .ma/.mb/.json validation samples found under Samples/.")
    if len(json_files) == 0:
        errors.append("No exporter JSON golden sample found. Add at least one sample under Samples/ExporterJson/.")

    manifest = {
        "tool": "MAYAtoUnity",
        "generatedBy": "scripts/portfolio_validation.py",
        "sampleCounts": {"ma": len(ma_files), "mb": len(mb_files), "json": len(json_files)},
        "samples": [rel(ctx, p) for p in ma_files + mb_files + json_files],
    }
    ctx.report_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = ctx.report_dir / "maya_sample_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    ctx.generated_samples.append(rel(ctx, manifest_path))
    return warnings, errors


def validate_golden_json(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    expected_files = sorted((ctx.root / "Samples/Expected").glob("*.expected.json"))
    if not expected_files:
        errors.append("No expected golden metrics found under Samples/Expected/.")
        return warnings, errors

    summaries = []
    for expected_path in expected_files:
        expected = json.loads(expected_path.read_text(encoding="utf-8"))
        sample_path = ctx.root / expected["sample"]
        if not sample_path.exists():
            errors.append(f"Golden sample referenced by {expected_path.name} does not exist: {expected['sample']}")
            continue
        sample = json.loads(sample_path.read_text(encoding="utf-8"))
        exp = expected.get("expected", {})
        meshes = sample.get("meshes") or []
        materials = sample.get("materials") or []
        nodes = sample.get("nodes") or []
        transforms = sample.get("transforms") or []
        total_vertices = 0
        total_triangles = 0
        schema_errors: List[str] = []
        for mesh_index, mesh in enumerate(meshes):
            vertices = mesh.get("vertices") or []
            indices = mesh.get("indices") or []
            if len(vertices) % 3 != 0:
                schema_errors.append(f"mesh[{mesh_index}] vertex float count is not divisible by 3")
            vertex_count = len(vertices) // 3
            if len(indices) % 3 != 0:
                schema_errors.append(f"mesh[{mesh_index}] index count is not divisible by 3")
            for i, idx in enumerate(indices):
                if not isinstance(idx, int) or idx < 0 or idx >= vertex_count:
                    schema_errors.append(f"mesh[{mesh_index}] index out of range at {i}: {idx}")
                    break
            total_vertices += vertex_count
            total_triangles += len(indices) // 3
        errors.extend(schema_errors)

        checks = {
            "schemaVersion": sample.get("schemaVersion"),
            "nodeCount": len(nodes),
            "transformCount": len(transforms),
            "meshCount": len(meshes),
            "materialCount": len(materials),
            "totalVertices": total_vertices,
            "totalTriangles": total_triangles,
        }
        for key, actual in checks.items():
            target = expected.get(key) if key == "schemaVersion" else exp.get(key)
            if target is not None and actual != target:
                errors.append(f"{expected_path.name}: {key} expected {target}, got {actual}")
        summaries.append({"expected": rel(ctx, expected_path), "sample": expected["sample"], "checks": checks, "schemaErrors": schema_errors})

    summary_path = ctx.report_dir / "maya_golden_validation.json"
    ctx.report_dir.mkdir(parents=True, exist_ok=True)
    summary_path.write_text(json.dumps({"goldenSamples": summaries}, indent=2, ensure_ascii=False), encoding="utf-8")
    ctx.generated_samples.append(rel(ctx, summary_path))
    return warnings, errors


def check_asmdefs(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    asmdefs = list((ctx.root / "Assets").glob("**/*.asmdef")) if (ctx.root / "Assets").exists() else []
    if not asmdefs:
        warnings.append("No .asmdef files found; large importer may remain in Assembly-CSharp.")
    for path in asmdefs:
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            errors.append(f"Invalid asmdef JSON: {rel(ctx, path)}: {exc}")
            continue
        if not data.get("name"):
            errors.append(f"asmdef missing name: {rel(ctx, path)}")
    return warnings, errors


def generate_dry_run_plan(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    ctx.rollback_plan.extend([
        "Validation is dry-run: no Unity assets, scenes, prefabs, or generated meshes are modified.",
        "If a future apply step generates import reports or temporary samples, delete Docs/Reports/* generated in the CI workspace to roll back.",
        "Importer runtime mutations must be reviewed through Unity validation reports before committing generated assets.",
    ])
    return warnings, errors


def write_reports(ctx: ValidationContext) -> Report:
    ctx.report_dir.mkdir(parents=True, exist_ok=True)
    report = Report(
        tool="MAYAtoUnity",
        dry_run=ctx.dry_run,
        success=not ctx.has_errors(),
        stages=ctx.stages,
        rollback_plan=ctx.rollback_plan,
        generated_samples=ctx.generated_samples,
        limitations_status=ctx.limitations_status,
    )

    json_path = ctx.report_dir / "portfolio_validation_report.json"
    md_path = ctx.report_dir / "portfolio_validation_report.md"

    json_path.write_text(json.dumps(asdict(report), indent=2, ensure_ascii=False), encoding="utf-8")

    lines = [
        "# MAYAtoUnity Portfolio Validation Report",
        "",
        f"Dry run: `{ctx.dry_run}`",
        f"Success: `{report.success}`",
        f"Limitations: {report.limitations_status}",
        "",
        "## Stage benchmark",
        "",
        "| Stage | Result | ms | Warnings | Errors |",
        "|---|---:|---:|---:|---:|",
    ]
    for stage in report.stages:
        lines.append(f"| {stage.name} | {'PASS' if stage.success else 'FAIL'} | {stage.milliseconds:.3f} | {len(stage.warnings)} | {len(stage.errors)} |")
    lines.extend(["", "## Generated sample/report artifacts", ""])
    lines.extend([f"- `{p}`" for p in report.generated_samples] or ["- none"])
    lines.extend(["", "## Rollback / dry-run plan", ""])
    lines.extend([f"- {item}" for item in report.rollback_plan])
    lines.extend(["", "## Errors and warnings", ""])
    for stage in report.stages:
        for warning in stage.warnings:
            lines.append(f"- WARNING [{stage.name}] {warning}")
        for error in stage.errors:
            lines.append(f"- ERROR [{stage.name}] {error}")
    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report-dir", default="Docs/Reports")
    parser.add_argument("--dry-run", action="store_true", default=True)
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[1]
    ctx = ValidationContext(root=root, report_dir=root / args.report_dir, dry_run=args.dry_run)

    ctx.run_stage("required_paths", lambda: require_paths(ctx))
    ctx.run_stage("readme_limitations", lambda: check_readme(ctx))
    ctx.run_stage("sample_manifest_generation", lambda: check_samples(ctx))
    ctx.run_stage("golden_json_validation", lambda: validate_golden_json(ctx))
    ctx.run_stage("asmdef_static_validation", lambda: check_asmdefs(ctx))
    ctx.run_stage("dry_run_rollback_plan", lambda: generate_dry_run_plan(ctx))

    report = write_reports(ctx)
    print((ctx.report_dir / "portfolio_validation_report.md").read_text(encoding="utf-8"))
    return 0 if report.success else 1


if __name__ == "__main__":
    sys.exit(main())
