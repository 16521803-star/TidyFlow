param(
    [string]$Version = "0.7.0",
    [string]$PackageName = "HLTPRODEV.TidyFlow-SmartFileOrganizer",
    [string]$Publisher = "CN=9E0D3E41-10E9-4314-AFD1-0F25C79E11C4",
    [string]$PublisherDisplayName = "HLTPRODEV",
    [string]$AppDisplayName = "TidyFlow - Smart File Organizer"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    # MSIX requires 4-part version: Major.Minor.Build.0
    $msixVersion = if ($Version -match '^\d+\.\d+\.\d+$') { "$Version.0" } else { $Version }

    # Locate makeappx.exe from Windows Kits or restore Microsoft.Windows.SDK.BuildTools via NuGet
    $makeAppx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $makeAppx) {
        $nugetRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools"
        $makeAppx = Get-ChildItem $nugetRoot -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not $makeAppx) {
        Write-Host "Restoring Microsoft.Windows.SDK.BuildTools to obtain makeappx.exe..." -ForegroundColor Cyan
        dotnet restore packaging/msix/SdkTools.csproj
        if ($LASTEXITCODE -ne 0) { throw "Failed to restore SdkTools.csproj." }

        $nugetRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.buildtools"
        $makeAppx = Get-ChildItem $nugetRoot -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not $makeAppx) {
        throw "Could not locate makeappx.exe."
    }

    Write-Host "Using makeappx.exe: $makeAppx" -ForegroundColor DarkGray

    $stagingDir = Join-Path $repoRoot "publish\msix-staging"
    $outDir = Join-Path $repoRoot "publish\msix"
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $stagingDir
    New-Item -ItemType Directory -Force -Path $stagingDir, $outDir | Out-Null

    Write-Host "Publishing TidyFlow.App (self-contained win-x64)..." -ForegroundColor Cyan
    dotnet publish src/TidyFlow.App/TidyFlow.App.csproj `
        -c Release -r win-x64 --self-contained true `
        -p:Version=$Version `
        -o $stagingDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    # Remove PDBs from Store MSIX payload to keep package clean
    Get-ChildItem $stagingDir -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue

    # Copy MSIX visual assets
    $assetsDst = Join-Path $stagingDir "Assets"
    New-Item -ItemType Directory -Force -Path $assetsDst | Out-Null
    Copy-Item "packaging/msix/Assets/*" $assetsDst -Force

    # Materialize AppxManifest.xml
    $xmlEscape = { param([string]$v) [System.Security.SecurityElement]::Escape($v) }
    $manifestContent = Get-Content "packaging/msix/AppxManifest.template.xml" -Raw
    $manifestContent = $manifestContent.Replace("{{PACKAGE_NAME}}", (& $xmlEscape $PackageName))
    $manifestContent = $manifestContent.Replace("{{PUBLISHER}}", (& $xmlEscape $Publisher))
    $manifestContent = $manifestContent.Replace("{{PUBLISHER_DISPLAY_NAME}}", (& $xmlEscape $PublisherDisplayName))
    $manifestContent = $manifestContent.Replace("{{APP_DISPLAY_NAME}}", (& $xmlEscape $AppDisplayName))
    $manifestContent = $manifestContent.Replace("{{VERSION}}", (& $xmlEscape $msixVersion))
    $manifestPath = Join-Path $stagingDir "AppxManifest.xml"
    [System.IO.File]::WriteAllText($manifestPath, $manifestContent, [System.Text.UTF8Encoding]::new($false))

    $msixOut = Join-Path $outDir "TidyFlow-$Version-x64.msix"
    Write-Host "Packing MSIX -> $msixOut" -ForegroundColor Cyan
    & $makeAppx pack /o /d $stagingDir /p $msixOut
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed." }

    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $stagingDir
    Write-Host "MSIX package ready: $msixOut" -ForegroundColor Green
}
finally {
    Pop-Location
}
