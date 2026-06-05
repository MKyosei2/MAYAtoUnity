#!/usr/bin/env python3
"""Repository-level portfolio validation for MAYAtoUnity.

This script is intentionally dry-run first. It does not mutate Unity assets.
It checks reviewer-critical files, sample coverage, README honesty, asmdef health,
and writes Markdown/JSON reports so CI failures are inspectable instead of silent.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
import traceback
from dataclasses import dataclass, asdict
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
        except Exception as exc:  # keep the failure inspectable in the report
            errors.append(f"Unhandled exception: {exc}")
            errors.append(traceback.format_exc())
        elapsed = (time.perf_counter() - start) * 1000.0
        self.stages.append(Stage(name=name, success=not errors, milliseconds=elapsed, warnings=warnings, errors=errors))

    def has_errors(self) -> bool:
        return any(not s.success for s in self.stages)


def require_paths(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    required = [
        "README.md",
        "Assets/MayaImporter",
        "Assets/MayaImporter/MayaUnityJsonImporter.cs",
        "Assets/MayaImporter/MayaUnityJsonRuntimeBuilder.cs",
        "Assets/MayaImporter/Editor",
        "Packages",
        "ProjectSettings",
        "Tools/MayaExporter",
        "Samples",
    ]
    for rel in required:
        if not (ctx.root / rel).exists():
            errors.append(f"Missing required path: {rel}")
    return warnings, errors


def check_readme(ctx: ValidationContext) -> tuple[list[str], list[str]]:
    warnings: List[str] = []
    errors: List[str] = []
    readme = ctx.root / "README.md"
    text = readme.read_text(encoding="utf-8") if readme.exists() else ""
    required_phrases = [
        "30-second overview",
        "Reviewer path",
        "Current limitations",
        "Roadmap",
        "Portfolio wording",
        "not a full replacement",
    ]
    for phrase in required_phrases:
        if phrase.lower() not in text.lower():
            errors.append(f"README is missing reviewer/limitation phrase: {phrase}")
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
        warnings.append("No exporter JSON golden sample found. Add at least one sample under Samples/ExporterJson/.")

    manifest = {
        "tool": "MAYAtoUnity",
        "generatedBy": "scripts/portfolio_validation.py",
        "sampleCounts": {"ma": len(ma_files), "mb": len(mb_files), "json": len(json_files)},
        "samples": [str(p.relative_to(ctx.root)).replace("\\", "/") for p in ma_files + mb_files + json_files],
    }
    ctx.report_dir.mkdir(parents=True, exist_ok=True)
    manifest_path = ctx.report_dir / "maya_sample_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    ctx.generated_samples.append(str(manifest_path.relative_to(ctx.root)).replace("\\", "/"))
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
            errors.append(f"Invalid asmdef JSON: {path}: {exc}")
            continue
        if not data.get("name"):
            errors.append(f"asmdef missing name: {path}")
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
    ctx.run_stage("asmdef_static_validation", lambda: check_asmdefs(ctx))
    ctx.run_stage("dry_run_rollback_plan", lambda: generate_dry_run_plan(ctx))

    report = write_reports(ctx)
    print((ctx.report_dir / "portfolio_validation_report.md").read_text(encoding="utf-8"))
    return 0 if report.success else 1


if __name__ == "__main__":
    sys.exit(main())
