# Unfold3D

A .NET-based toolchain for simplifying 3D meshes, unfolding them to 2D, and exporting cut-ready plans for fabrication (e.g., laser cutters). This repository includes the core engine, UI, and command‑line entry points used to load meshes, apply simplification, compute seam placement and unfolding, then emit 2D layouts with part IDs, fold types, and assembly annotations.

---

## Table of Contents

* [Overview](#overview)
* [Key Features](#key-features)
* [Architecture at a Glance](#architecture-at-a-glance)
* [System Requirements](#system-requirements)
* [Getting Started](#getting-started)

  * [Prerequisites](#prerequisites)
  * [Clone & Restore](#clone--restore)
  * [Build](#build)
  * [Run (CLI)](#run-cli)
  * [Run (GUI)](#run-gui)
* [Configuration](#configuration)
* [Using Unfold3D](#using-unfold3d)

  * [Typical Workflow](#typical-workflow)
  * [Supported Formats](#supported-formats)
  * [Command Examples](#command-examples)
* [Output](#output)
* [Project Structure](#project-structure)
* [Testing](#testing)
* [Publish / Distribute](#publish--distribute)
* [Troubleshooting](#troubleshooting)
* [Contributing](#contributing)
* [License](#license)

---

## Overview

Unfold3D implements the pipeline described in the Software Design Document (SDD):

1. **Mesh ingestion & display**
2. **Mesh simplification** (e.g., edge-collapse / quadric error metrics)
3. **Seam selection & unfolding to 2D**
4. **Layout packing** to fit sheets/bed sizes
5. **Exporter** producing fabrication files (DXF/SVG/PDF) with cut/score instructions and assembly hints

The goal is to provide a consistent, reproducible path from 3D input → simplified 2D plans suitable for laser cutting and accurate reassembly.

## Key Features

* Import common mesh formats (e.g., OBJ, STL, PLY)
* Deterministic simplification with tunable error budgets
* Automatic seam placement with heuristics (curvature, geodesic distance, part size)
* Non-overlapping 2D unwrap with layout packing
* Export to **DXF**, **SVG**, and **PDF** with layers for cut/score/labels
* CLI for batch jobs; optional desktop UI for interactive preview

## Architecture at a Glance

> Names may differ slightly depending on your solution. Adjust paths as needed.

```
UnBox3D.sln
├─ src/
│  ├─ UnBox3D/                 # App entry (CLI and/or UI host)
│  ├─ UnBox3D.Core/            # Algorithms: simplify, unwrap, pack, exporters
│  ├─ UnBox3D.IO/              # Readers/Writers for OBJ/STL/PLY, DXF/SVG/PDF
│  ├─ UnBox3D.UI/              # (Optional) Desktop UI (WPF/WinUI/Avalonia)
│  └─ UnBox3D.Tests/           # Unit & integration tests
└─ assets/                     # Sample meshes, fonts, templates
```

* **Core** encapsulates mesh ops and unfolding algorithms.
* **IO** contains importers/exporters and format adapters.
* **UI** (if present) provides viewport rendering (3D/2D) and workflow controls.
* **App** wires everything together with DI, config, logging, and CLI commands.

## System Requirements

* **.NET 8 SDK** or newer
* Windows 10/11 (x64) recommended; Linux/macOS supported for CLI
* For UI builds on Linux/macOS, ensure your chosen UI framework is supported

Verify your setup:

```bash
dotnet --info
```

## Getting Started

### Prerequisites

* Install [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
* (Optional) **JetBrains Rider** or **Visual Studio 2022** / **VS Code**

### Clone & Restore

```bash
git clone <repo-url> Unfold3D
cd Unfold3D
 dotnet restore
```

### Build

```bash
# Build the whole solution
 dotnet build -c Release

# Or build a specific project (adjust path if different)
 dotnet build src/UnBox3D/UnBox3D.csproj -c Release
```

### Run (CLI)

```bash
# From repo root (adjust project path if needed)
 dotnet run --project src/UnBox3D/UnBox3D.csproj -- \
  --input assets/samples/bunny.obj \
  --target-faces 5000 \
  --sheet-width 600 --sheet-height 400 \
  --export dxf --out ./out/bunny
```

### Run (GUI)

If a desktop UI project is included (e.g., `UnBox3D.UI`):

```bash
 dotnet run --project src/UnBox3D.UI/UnBox3D.UI.csproj -c Debug
```

Or use **Rider**: open the solution, choose the UI project run configuration, and press ▶️.

## Configuration

Runtime settings can be supplied via command-line flags or `appsettings.json` next to the app:

```json
{
  "Unfold3D": {
    "Simplification": {
      "TargetFaceCount": 5000,
      "PreserveBoundaries": true
    },
    "Unwrap": {
      "SeamHeuristic": "Curvature",
      "PackPaddingMm": 2.0,
      "Sheet": { "WidthMm": 600, "HeightMm": 400 }
    },
    "Export": {
      "Format": "DXF",  
      "IncludeLabels": true,
      "LayerNames": { "Cut": "CUT", "Score": "SCORE", "Text": "TEXT" }
    }
  }
}
```

You can also use environment variables (e.g., `Unfold3D__Export__Format=SVG`).

## Using Unfold3D

### Typical Workflow

1. **Import** a mesh (OBJ/STL/PLY)
2. **Simplify** to target face count or error threshold
3. **Mark/auto-select seams** and **unwrap** to 2D islands
4. **Pack** islands into sheets using bed size & margins
5. **Export** DXF/SVG/PDF with cut/score layers and labels

### Supported Formats

* **Input**: OBJ, STL, PLY (extendable)
* **Output**: DXF (R12+), SVG, PDF

### Command Examples

```bash
# Minimal: import & export with defaults
 dotnet run --project src/UnBox3D/UnBox3D.csproj -- \
  --input ./assets/samples/box.stl --export svg --out ./out/box

# Set target faces and sheet size
 dotnet run --project src/UnBox3D/UnBox3D.csproj -- \
  --input ./assets/samples/bunny.obj \
  --target-faces 8000 \
  --sheet-width 500 --sheet-height 300 \
  --export dxf --out ./out/bunny

# Batch process a folder of meshes
 dotnet run --project src/UnBox3D/UnBox3D.csproj -- \
  --input ./assets/batch --recursive --export pdf --out ./out/batch
```

Common flags (adjust to your CLI implementation):

```
--input <fileOrFolder>
--target-faces <int> | --max-error <float>
--sheet-width <mm> --sheet-height <mm> --pack-padding <mm>
--export <dxf|svg|pdf> --out <path>
--labels on|off --layers <cutLayer,scoreLayer,textLayer>
--headless (for CI)
--verbose
```

## Output

Generated files are placed under the `--out` directory:

* `*.dxf`, `*.svg`, or `*.pdf` – cut-ready plans
* `manifest.json` – metadata (parts, seams, fold types, scales) (optional)
* `preview.png` – quick 2D snapshot (optional)

## Project Structure

```
src/
  UnBox3D/            # Program.cs, CLI verbs, DI, logging
  UnBox3D.Core/       # Geometry, simplification, unwrap, packing
  UnBox3D.IO/         # Parsers & exporters
  UnBox3D.UI/         # Desktop app (if included)
  UnBox3D.Tests/      # xUnit/NUnit tests
assets/
  samples/            # Example meshes
out/                  # Build & export artifacts (gitignored)
```

## Testing

```bash
 dotnet test
```

## Publish / Distribute

Create a platform-specific build:

```bash
# Windows x64, framework-dependent
 dotnet publish src/UnBox3D/UnBox3D.csproj -c Release -r win-x64 --no-self-contained -o ./publish/win

# Self-contained (no runtime required)
 dotnet publish src/UnBox3D/UnBox3D.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish/win-sc

# Linux & macOS variants
 dotnet publish src/UnBox3D/UnBox3D.csproj -c Release -r linux-x64   -o ./publish/linux
 dotnet publish src/UnBox3D/UnBox3D.csproj -c Release -r osx-arm64   -o ./publish/osx
```

> **Tip:** Ensure the path to your `.csproj` is correct. From solution root, it is commonly `src/UnBox3D/UnBox3D.csproj`. If you see `MSB1009: Project file does not exist`, verify the relative path or provide an absolute path.

## Troubleshooting

* **MSBUILD : error MSB1009: Project file does not exist.**

  * Run `dir /s *.csproj` (Windows) or `find . -name "*.csproj"` to locate the actual project path.
  * Use that exact path in `dotnet run --project <path>` or `dotnet publish <path>`.
  * Avoid mixing `\` and `/` incorrectly on Windows; quotes can help: `"src/UnBox3D/UnBox3D.csproj"`.

* **Cannot load mesh / format not recognized**

  * Confirm the file extension and that the importer supports it.
  * Try triangulating meshes and removing non-manifold edges in your DCC tool first.

* **Overlapping islands in 2D**

  * Increase `--pack-padding` and/or adjust sheet size.
  * Reduce target faces before unwrap.

* **Wrong scale in output**

  * Ensure units are consistent (mm). Some OBJ/STL files are unitless.
  * Use a known reference part or set an explicit scale flag.

* **UI doesn’t run on non‑Windows**

  * Some UI stacks (WPF) are Windows-only. Use CLI on Linux/macOS or switch to Avalonia/WinUI if cross‑platform UI is required.

## Contributing

1. Fork the repo and create a feature branch
2. Add tests for new functionality
3. Ensure `dotnet format`/linters pass
4. Open a PR with a clear description and screenshots for UI changes

## License

Specify your license here (e.g., MIT). Add `LICENSE` at the repo root.
