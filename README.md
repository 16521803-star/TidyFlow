# TidyFlow

A fast, open-source Windows desktop utility to **organize messy folders** (like `Downloads`) and **batch-rename files from custom lists or rules** — with mandatory Live Preview and 1-Click Undo.

---

## Features

- **Organize Folder:** Sort cluttered directories into clean subfolders by file category (`Documents`, `Images`, `Videos`, `Installers`, `Archives`, `Code`, `Others`), date (`YYYY-MM`), or saved keyword/extension rules.
- **List-Based Batch Rename:** Add or drag-and-drop files from Explorer, reorder them, and rename them 1-to-1 from a pasted/saved list of names (preserving extensions, sanitizing invalid characters, and resolving duplicates safely).
- **Quick Template Rename:** Format names with `{index}`, `{name}`, `{date}`, `{ext}`, Find & Replace, and duplicate marker cleanup (`(1)`, `Copy of`).
- **Live Preview (Dry-Run):** Inspect every `Current -> New` change before any file is touched.
- **1-Click Undo:** Persistent JSON transaction log rolls back batch moves/renames safely (including cyclic swaps and case-only renames).
- **Auto-Updates:** Built-in updates via GitHub Releases powered by **Velopack**.

---

## Download & Install (Windows)

Grab the latest build from the **[Releases](https://github.com/16521803-star/TidyFlow/releases)** page:
- **Installer (`Setup.exe`):** Recommended — installs TidyFlow and enables 1-click in-app updates.
- **Portable (`TidyFlow-win-x64-portable.exe`):** Single-file executable; download and run directly without installing.

---

## Project Structure

```text
src/
  TidyFlow.Core/        # Pure C# file organization, renaming & safety engine
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

### Publish Standalone Single-File `.exe`
```powershell
dotnet publish src/TidyFlow.App/TidyFlow.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
```

### Creating a New GitHub Release
Push a version tag matching `v*` to trigger [release.yml](.github/workflows/release.yml):
```powershell
git tag v0.3.0
git push origin v0.3.0
```

---

## License

[MIT](LICENSE)
