# TidyFlow — Session & Change Log

> Purpose: Persistent memory of architectural decisions, environment facts, edits made per session, and next steps so frequent feature upgrades stay consistent.

---

## Session 01 — 2026-11-24 (Project Kickoff & Planning)

### Decisions Made
- **Product Name / Repo:** `TidyFlow` (`https://github.com/16521803-star/TidyFlow.git`)
- **Target Audience:** Public open-source Windows users via GitHub Releases, with frequent feature updates.
- **Tech Stack Selected:** **C# / .NET 8 (WPF + WPF-UI)** + **Velopack** (auto-update) + **GitHub Actions** (CI/CD).
  - *Why over Python/PySide6:* Avoids large PyInstaller bundles and frequent Windows Defender/SmartScreen false positives for end-users.
  - *Why over Tauri:* Faster Windows-native integration (Explorer drag-drop, tray, context menu) and lower setup friction once `.NET 8 SDK` is installed.
- **Core MVP Scope:**
  1. Reorder/organize messy folders (e.g., `Downloads`) into structured subfolders by type/date/rules.
  2. Batch-rename selected files based on a user-chosen/pasted list of names.
  3. Mandatory **Live Preview** and **Transaction Undo** for safety.

### Environment Check (Local Machine)
- OS: Windows
- Workspace path: `d:\Working\workspaces\SmartToolForPC`
- Installed tools verified:
  - `git`: `2.40.0.windows.1`
  - `winget`: `v1.29.380`
  - `python`: `3.12.10`
  - `node`: `v24.16.0`
- Missing tools needed for Phase 0:
  - `.NET 8 SDK` (`dotnet --list-sdks` currently empty; runtime only)
  - `gh` CLI (optional, not required)

### Files Created / Modified
- Created [PLAN.md](file:///d:/Working/workspaces/SmartToolForPC/PLAN.md) — Full architecture, phased roadmap, and safety rules for TidyFlow (Phase 0 & Phase 1 complete).
- Created [SESSION_LOG.md](file:///d:/Working/workspaces/SmartToolForPC/SESSION_LOG.md) — This persistent session & edit tracker.
- Initialized Git (`main`) and pushed Phase 0 commit `f57b950` to `https://github.com/16521803-star/TidyFlow.git`.
- Created [.gitignore](file:///d:/Working/workspaces/SmartToolForPC/.gitignore), [LICENSE](file:///d:/Working/workspaces/SmartToolForPC/LICENSE), [README.md](file:///d:/Working/workspaces/SmartToolForPC/README.md), and [.github/workflows/ci.yml](file:///d:/Working/workspaces/SmartToolForPC/.github/workflows/ci.yml).
- Installed `.NET 8 SDK` (`8.0.428`) and scaffolded [TidyFlow.sln](file:///d:/Working/workspaces/SmartToolForPC/TidyFlow.sln):
  - `src/TidyFlow.Core/TidyFlow.Core.csproj` (`net8.0`)
  - `src/TidyFlow.App/TidyFlow.App.csproj` (`net8.0-windows`, `WPF-UI 4.3.0`, references `TidyFlow.Core`)
  - `tests/TidyFlow.Core.Tests/TidyFlow.Core.Tests.csproj` (`net8.0`, xUnit, references `TidyFlow.Core`)
- Implemented **Phase 1 (`TidyFlow.Core`)** & unit tests (`tests/TidyFlow.Core.Tests`) — commit `b406ec7`:
  - Models: `FileEntry.cs`, `OperationPlan.cs`
  - Scanning: `FolderScanOptions.cs`, `FolderScanner.cs`
  - Organizing: `FileCategoryRegistry.cs`, `OrganizeOptions.cs`, `FolderOrganizer.cs`
  - Renaming: `FilenameSanitizer.cs`, `ListRenameOptions.cs`, `BatchRenamer.cs`
  - Safety & Execution: `ConflictResolver.cs`, `TransactionModels.cs`, `BatchExecutor.cs` (two-phase staging for cyclic/case-only renames + JSON Undo history)
  - Presets: `PresetStore.cs`
  - Tests: `TempTestWorkspace.cs`, `FolderScannerTests.cs`, `FolderOrganizerTests.cs`, `BatchRenamerTests.cs`, `BatchExecutorTests.cs`
- Implemented **Phase 2 (`TidyFlow.App` with WPF-UI 4.3.0)** — commit `d955108`:
  - `Models/PreviewOperationRow.cs`, `ViewModels/ObservableObject.cs`, `ViewModels/OrganizeViewModel.cs`, `ViewModels/RenameViewModel.cs`, `ViewModels/MainViewModel.cs`
  - `App.xaml`, `MainWindow.xaml` & `MainWindow.xaml.cs` (`FluentWindow`, `Organize Folder` tab, `List Batch Rename` tab, Explorer drag & drop, live Dry-Run preview grids, confirmation dialogs, saved presets/lists, and header Undo button).
- Implemented **Phase 3 (Release Packaging & Auto-Update)** — commit `2b9f2ce`:
  - Switched `MainWindow.xaml` backdrop to `None` with explicit `ApplicationBackgroundBrush` for reliable rendering across Windows 10/11 setups; added `VelopackApp.Build().Run()` and crash logging in `App.xaml.cs`.
  - Added `Velopack 1.2.161` + `Services/AppUpdateService.cs` + header `Updates (v0.3.0)` button in `MainViewModel.cs` & `MainWindow.xaml`.
  - Created [.github/workflows/release.yml](file:///d:/Working/workspaces/SmartToolForPC/.github/workflows/release.yml) to build both `TidyFlow-win-x64-portable.exe` and Velopack installer packages on `v*` tags.
  - Updated [README.md](file:///d:/Working/workspaces/SmartToolForPC/README.md) and published standalone `win-x64` executable to `publish/TidyFlow.App.exe` (verified `11/11` unit tests pass).
- **UI & Rename Destination Update (from user screenshot review)** — commit `ba12f67`:
  - Added optional `OutputDirectory` to `ListRenameOptions`, `TemplateRenameOptions`, `BatchRenamer`, and `RenameViewModel` (`Save to:` folder picker + `Clear` button in **List Batch Rename**), with `OperationKind.MoveAndRename` support and 2 new unit tests (`13/13` total).
  - Fixed `DataGrid` dark/light theme styling (`PreviewGridStyle` with `DynamicResource` brushes so rows & headers render clearly instead of light grey box) and added `VerticalContentAlignment="Stretch"` + `MinHeight="96"` on the right-side multi-line name list editor.
- **Phase 4.0 & 4.1 (`v0.4.0`)** — commit `157a239` (tag `v0.4.0` pushed):
  - Created `assets/logo.png` and `assets/tidyflow.ico`; embedded across `.csproj`, `MainWindow`, `README.md`, `Program.cs`, and Velopack `TidyFlow-win-Setup.exe`.
  - Added `RuleMatchLogic` (`MatchAny`/`MatchAll`), `MinimumAgeDays`, `DuplicateFinder.cs`, `SpaceCleaner.cs`, `RecycleBinService.cs`, and UI tabs (`Duplicate Finder`, `Space Cleaner`).
- **Phase 4.2 (`v0.5.0`)** — commit `352f0c2` (tag `v0.5.0` pushed):
  - Core (`TidyFlow.Core`): Added `ExifDateReader.cs`, `FolderWatchService.cs`, and `ImageResizePlanner.cs`.
  - App (`TidyFlow.App`): Added `ImageResizerService.cs`, `ExplorerContextMenuService.cs`, `TrayIconService.cs`, `ImageResizerViewModel.cs`, `AutomationViewModel.cs`, and UI tabs (`Image Resizer`, `Automation & Tools`).
- **Phase 4.3 (`v0.6.0`)** — commit `af7eda9` (tag `v0.6.0` pushed):
  - Added `WindowsOcrService.cs`, `OcrNameSuggester.cs`, `AiOrganizePlanner.cs`, `AiSettingsStore.cs`, `SmartAiViewModel.cs`, and **Tab 7 (`Smart OCR & AI`)**.
- **`v0.6.1` Fixes & Image Resizer Upgrades** — commit `5e727c2` (tag `v0.6.1` pushed):
  - Added `CropToExact` & `ExactStretch` modes, labeled UI controls, `Apply Smart Plan` fix, and instruction stopwords filtering.
- **`v0.7.0` Multi-Language UI & About Dialog** — commit `dadbcc1` (tag `v0.7.0` pushed):
  - Added `LocalizationService.cs`, `Strings.en/vi/ja.xaml`, `AboutWindow.xaml`, and `15s` update check timeout.
- **Microsoft Store Packaging & Privacy Policy (`MSIX`):**
  - Added [PRIVACY.md](file:///d:/Working/workspaces/SmartToolForPC/PRIVACY.md) for Store submission compliance.
  - Generated MSIX tile assets (`packaging/msix/Assets/*`) and Partner Center listing logos (`packaging/store-listings/Logo300x300.png`, `Logo1000x1000.png`).
  - Added `packaging/msix/AppxManifest.template.xml`, `SdkTools.csproj`, and `scripts/pack-msix.ps1` using `makeappx.exe` (`Microsoft.Windows.SDK.BuildTools`); verified output `publish/msix/TidyFlow-0.7.0-x64.msix` (`~77.3 MB`).

### Next Steps
1. Register/verify account at `https://storedeveloper.microsoft.com` and reserve app name in Partner Center.
2. Re-run `.\scripts\pack-msix.ps1` with the exact Partner Center `PackageName` and `Publisher` GUID, then upload `TidyFlow-0.7.0-x64.msix` + Store logos + `PRIVACY.md` link.
