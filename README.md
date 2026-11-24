<p align="center">
  <img src="assets/logo.png" alt="TidyFlow Logo" width="96" height="96" />
</p>

<h1 align="center">TidyFlow</h1>

<p align="center">
  A fast, open-source Windows desktop utility to <strong>organize messy folders</strong>, <strong>batch-rename files from custom lists</strong>, <strong>remove duplicate files</strong>, and <strong>clean up disk space</strong> — with mandatory Live Preview and safe Undo / Recycle Bin protection.
</p>

---

## Features

- **Organize Folder:** Sort cluttered directories (`Downloads`, etc.) into clean subfolders by file category, date (`YYYY-MM`), or custom rules with **`MatchAny (OR)` / `MatchAll (AND)`** logic and **minimum file age** filters.
- **List-Based Batch Rename:** Add or drag-and-drop files from Explorer, reorder them, rename 1-to-1 from a pasted/saved list of names, and optionally move renamed files into a chosen output folder (`Save to:`).
- **Quick Template Rename:** Format names with `{index}`, `{name}`, `{date}`, `{ext}`, Find & Replace, and duplicate marker cleanup (`(1)`, `Copy of`).
- **Duplicate File Finder:** Fast size + **SHA-256** content scan with one-click *Keep Oldest* / *Keep Newest* presets and safe **Recycle Bin** cleanup.
- **Space & Clutter Cleaner:** Find the top 100 largest files, inspect subfolder usage, and remove nested empty folders safely.
- **Live Preview & 1-Click Undo:** Inspect every `Current -> New` change before applying, with persistent JSON transaction rollback for moves & renames.
- **Auto-Updates:** Built-in updates via GitHub Releases powered by **Velopack**.

---

## Download & Install (Windows)

Grab the latest build from the **[Releases](https://github.com/16521803-star/TidyFlow/releases)** page:
- **Installer (`TidyFlow-win-Setup.exe`):** Recommended — installs TidyFlow with Desktop & Start Menu shortcuts and enables 1-click in-app updates.
- **Portable (`TidyFlow-win-x64-portable.exe`):** Single-file executable; download and run directly without installing.

---

## Project Structure

```text
assets/                 # App logo PNG & multi-resolution Windows ICO
scripts/                # Local packaging helper (pack-local.ps1)
src/
  TidyFlow.Core/        # Pure C# organizing, renaming, duplicate, cleaner & safety engine
  TidyFlow.App/         # Windows WPF + WPF-UI desktop application + Velopack
tests/
  TidyFlow.Core.Tests/  # xUnit unit tests
```

---

## Building from Source

### Prerequisites
- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Commands
```powershell
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet run --project src/TidyFlow.App
```

### Publish Portable `.exe` & Windows Installer Locally
```powershell
dotnet tool install -g vpk
.\scripts\pack-local.ps1 -Version 0.4.0
```

### Creating a New GitHub Release
Push a version tag matching `v*` to trigger [release.yml](.github/workflows/release.yml):
```powershell
git tag v0.4.0
git push origin v0.4.0
```

---

## License

[MIT](LICENSE)
