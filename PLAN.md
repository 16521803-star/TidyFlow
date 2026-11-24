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
- [ ] **Safe Scanner:** Enumerate files with filters; skip in-progress downloads (`.crdownload`, `.part`, `.tmp`) and hidden/system files by default
- [ ] **Organize Engine:**
  - Built-in categories (`Documents`, `Images`, `Videos`, `Audio`, `Installers`, `Archives`, `Code`, `Others`)
  - Date grouping (`YYYY-MM` or `YYYY/MM`)
  - Custom keyword/extension -> target folder rules
- [ ] **List-Based Rename Engine:**
  - Map an ordered list of names 1-to-1 onto selected files
  - Preserve original file extension automatically (optional toggle)
  - Sanitize invalid Windows filename characters (`\ / : * ? " < > |`)
  - Handle count mismatch gracefully (warn, skip extras, or apply fallback pattern)
  - Optional template helpers (`{index}`, `{date}`, prefix/suffix, clean `(1)`/`Copy of`)
- [ ] **Preview & Conflict Resolver:** Generate `OperationPlan` (`SourcePath` -> `DestinationPath`, status/warnings, auto-numbering collisions without overwriting)
- [ ] **Transaction & Undo Manager:** Save executed batch operations to local JSON history so users can roll back moves/renames safely

### Phase 2 — Desktop UI (`TidyFlow.App` with WPF-UI)
- [ ] Modern Fluent Windows 11 shell (Dark/Light theme)
- [ ] **Organize Tab:** Pick folder (quick button for `Downloads`), choose/customize grouping rules, inspect Live Preview tree/table, Apply & Undo
- [ ] **List Rename Tab:**
  - Left pane: selected files (drag-and-drop from Explorer + move up/down or sort)
  - Right pane: paste/type/load list of target names (supports Excel/Notepad newline paste)
  - Bottom/Live Preview grid highlighting exact `Old Name -> New Name` + conflicts
- [ ] Saved Presets & Name Lists manager

### Phase 3 — Open-Source Release & Auto-Updates
- [ ] Integrate **Velopack** for 1-click in-app updates from GitHub Releases
- [ ] Add GitHub Actions Release workflow triggered on `v*` tags
- [ ] Write user-facing `README.md` with screenshots, download guide, and contribution notes

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
