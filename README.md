# TidyFlow

A fast, open-source Windows desktop utility to **organize messy folders** (like `Downloads`) and **batch-rename files from custom lists or rules** — with mandatory Live Preview and 1-Click Undo.

> Status: **Early Development (Phase 0)** — See [PLAN.md](PLAN.md) and [SESSION_LOG.md](SESSION_LOG.md).

---

## Planned Features

- **Folder Organizer:** Sort cluttered directories into clean subfolders by file type, date, or custom keyword rules.
- **List-Based Batch Rename:** Select files, adjust their order, and rename them 1-to-1 from a pasted or saved list of names (while preserving extensions and handling duplicates safely).
- **Live Preview (Dry-Run):** Inspect every `Current -> New` change before any file is touched.
- **1-Click Undo:** Persistent transaction log to roll back batch moves or renames.
- **Auto-Updates:** Built-in updates via GitHub Releases (planned with Velopack).

---

## Project Structure

```text
src/
  TidyFlow.Core/        # Pure C# file organization, renaming & safety engine
  TidyFlow.App/         # Windows WPF + WPF-UI desktop application
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

---

## License

[MIT](LICENSE)
