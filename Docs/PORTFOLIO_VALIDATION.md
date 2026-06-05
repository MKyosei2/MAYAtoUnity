# MAYAtoUnity Portfolio Validation

This document is the reviewer-facing validation entry point.

## Local dry-run validation

Linux / macOS:

```bash
bash scripts/run_portfolio_validation.sh
```

Windows PowerShell:

```powershell
./scripts/run_portfolio_validation.ps1
```

Generated reports:

```text
Docs/Reports/portfolio_validation_report.md
Docs/Reports/portfolio_validation_report.json
Docs/Reports/maya_sample_manifest.json
Docs/Reports/maya_golden_validation.json
```

## What is validated without Unity

The dry-run validation checks:

```text
- reviewer-critical files exist
- README includes explicit limitations
- exporter JSON golden sample exists
- expected metrics exist
- schemaVersion matches expected value
- node / transform / mesh / material counts match expected values
- vertex float counts are valid
- triangle index counts are valid
- mesh indices do not exceed vertex count
- asmdef files are valid JSON and named
- rollback / dry-run plan is reported
```

## Unity validation

For full Unity validation, open the project in Unity and run:

```text
Tools/MAYAtoUnity/Validate All Samples
```

If Unity license secrets are configured, the manual GitHub Actions workflow can run EditMode tests:

```text
Unity EditMode Tests
```

## Why this matters

The portfolio goal is not just to show an importer exists. The goal is to show that DCC importer behavior is reproducible, measurable, and inspectable through golden samples, schema validation, import profiling, cache statistics, and generated reports.
