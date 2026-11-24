# TidyFlow — Project Plan

> **Repository:** https://github.com/16521803-star/TidyFlow  
> **Platform:** Windows (x64)  
> **Tech Stack:** C# / .NET 8 · WPF + WPF-UI · xUnit · Velopack · GitHub Actions

---

## 1. Vision & Core Problem

**TidyFlow** helps Windows users quickly clean up messy folders (especially `Downloads`) and batch-rename files safely:
1. **Reorder / Organize into Folders:** Group files automatically or on-demand into clean folders by file type, date, or custom rules.
2. **List-Based Batch Rename:** Select multiple files, reorder them if needed, and rename them in one shot from a chosen/pasted list of names (or naming templates) with **Live Preview** and **1-Click Undo**.

---

## 2. Architecture (Designed for Frequent Upgrades)

```text
TidyFlow/
├── .github/
│   └── workflows/          # CI tests + automated GitHub Release builds
├── src/
│   ├── TidyFlow.Core/      # Pure C# engine (zero UI dependency)
│   │   ├── Scanning/       # Folder scan, filters, ignore active downloads
│   │   ├── Organizing/     # Category rules, date rules, custom keyword rules
│   │   ├── Renaming/       # List-to-file mapping, templates, sanitization
│   │   ├── Safety/         # Conflict resolver, dry-run preview, Undo log
│   │   └── Presets/        # Save/load user rules & reusable name lists (JSON)
│   └── TidyFlow.App/       # WPF + WPF-UI desktop app + Velopack auto-updater
├── tests/
│   └── TidyFlow.Core.Tests/ # Unit tests for file ops, collisions & rollback
├── PLAN.md
├── SESSION_LOG.md
├── LICENSE                 # MIT
└── README.md
```

**Why this split?**
- All file operations are tested in `TidyFlow.Core.Tests` before touching real user files.
- Adding new features (new sort rules, regex, EXIF/PDF naming, tray watcher) stays isolated inside `TidyFlow.Core` without breaking existing workflows.

---

## 3. Implementation Roadmap

### Phase 0 — Local Setup & GitHub Bootstrap
- [x] Install `.NET 8 SDK` (`winget install Microsoft.DotNet.SDK.8` -> `8.0.428`)
- [x] Initialize Git repo locally and link `origin` to `https://github.com/16521803-star/TidyFlow.git`
- [x] Add `.gitignore` (Visual Studio / .NET), `LICENSE` (MIT), `README.md`
- [x] Scaffold `TidyFlow.sln` (`TidyFlow.Core`, `TidyFlow.App` with `WPF-UI 4.3.0`, `TidyFlow.Core.Tests`)
- [x] Add GitHub Actions CI workflow (`.github/workflows/ci.yml`: build + test on push/PR)

### Phase 1 — Core Engine & Safety Layer (`TidyFlow.Core`)
- [x] **Safe Scanner:** Enumerate files with filters; skip in-progress downloads (`.crdownload`, `.part`, `.tmp`, `.opdownload`, `.download`) and Office `~$` lock files / hidden-system files by default
- [x] **Organize Engine:**
  - Built-in categories (`Documents`, `Images`, `Videos`, `Audio`, `Installers`, `Archives`, `Code`, `Others`)
  - Date grouping (`YYYY-MM`, `YYYY/MM`, or `YYYY`)
  - Custom keyword/extension -> target folder rules
- [x] **List-Based Rename Engine:**
  - Map an ordered list of names 1-to-1 onto selected files
  - Preserve original file extension automatically (optional toggle)
  - Sanitize invalid Windows filename characters (`\ / : * ? " < > |`) & reserved names (`CON`, `NUL`, etc.)
  - Handle count mismatch gracefully (skip extras or pad with original name / prefix + index)
  - Optional template helpers (`{index}`, `{name}`, `{date}`, `{ext}`, Find/Replace, clean `(1)`/`Copy of`)
- [x] **Preview & Conflict Resolver:** Generate `OperationPlan` (`SourcePath` -> `DestinationPath`, status/notes, auto-numbering collisions without overwriting)
- [x] **Transaction & Undo Manager:** Save executed batch operations to local JSON history (`BatchExecutor`), cycle-safe two-phase move staging, and `UndoLatest()` rollback with empty folder cleanup
- [x] **Preset Store:** JSON persistence (`PresetStore`) for custom organize rules and saved name lists

### Phase 2 — Desktop UI (`TidyFlow.App` with WPF-UI)
- [x] Modern Fluent Windows 11 shell (Dark/Light theme toggle + Mica backdrop + InfoBar feedback)
- [x] **Organize Tab:** Pick folder (quick button for `Downloads` + Drag & Drop), choose/customize grouping rules, inspect Live Preview table, Apply (with confirmation) & 1-click Undo
- [x] **List Rename Tab:**
  - Left pane: selected files (drag-and-drop from Explorer, Add Files/Folder, Move Up/Down, Sort A-Z/Date)
  - Right pane: paste/type/load list of target names (supports Excel/Notepad newline paste & Clipboard button) + Quick Template mode (`{index}`, `{name}`, Find/Replace)
  - Bottom/Live Preview grid highlighting exact `Old Name -> New Name` + conflicts
- [x] Saved Presets & Name Lists manager integrated with `PresetStore`

### Phase 3 — Open-Source Release & Auto-Updates
- [x] Integrate **Velopack** (`1.2.161`) for 1-click in-app updates from GitHub Releases (`AppUpdateService` + `Updates` header button)
- [x] Add GitHub Actions Release workflow (`.github/workflows/release.yml`) triggered on `v*` tags (builds portable single-file `.exe` + Velopack `vpk pack` installer)
- [x] Write user-facing `README.md` with download guide, build steps, and release tag instructions

### Phase 4 — Future Upgrades (Post-v1.0)
- [ ] Optional **Watch Folder** mode (System Tray background auto-tidy for `Downloads`)
- [ ] Windows Explorer context-menu integration (*"Send to TidyFlow"*)
- [ ] Smart metadata helpers (EXIF photo dates, PDF title extraction)

---

## 4. Key Design Rules (Non-Negotiable)
1. **Never modify files without a Dry-Run Preview first.**
2. **Never overwrite an existing file silently.** Always detect collisions and resolve or warn.
3. **Every applied batch must be recorded in the Undo log** (unless explicitly disabled for huge runs).
4. **Ignore partial/locked browser downloads** by default.
