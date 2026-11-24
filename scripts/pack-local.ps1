param(
    [string]$Version = "0.4.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    Write-Host "Running tests..." -ForegroundColor Cyan
    dotnet test TidyFlow.sln --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

    Write-Host "Publishing portable single-file EXE..." -ForegroundColor Cyan
    dotnet publish src/TidyFlow.App/TidyFlow.App.csproj `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:Version=$Version `
        -o publish

    if (Get-Command vpk -ErrorAction SilentlyContinue) {
        Write-Host "Packing Windows Setup installer via Velopack..." -ForegroundColor Cyan
        $vpkSrc = Join-Path $repoRoot "publish/velopack-src"
        $vpkOut = Join-Path $repoRoot "publish/installer"
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $vpkSrc, $vpkOut

        dotnet publish src/TidyFlow.App/TidyFlow.App.csproj `
            -c Release -r win-x64 --self-contained true `
            -p:Version=$Version `
            -o $vpkSrc

        vpk pack `
            --packId TidyFlow `
            --packTitle "TidyFlow" `
            --packVersion $Version `
            --runtime win-x64 `
            --packDir $vpkSrc `
            --mainExe TidyFlow.App.exe `
            --icon (Join-Path $repoRoot "assets/tidyflow.ico") `
            --outputDir $vpkOut

        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $vpkSrc
        Write-Host "Installer ready in: $vpkOut" -ForegroundColor Green
    }
    else {
        Write-Host "Velopack CLI (vpk) not found. Install with: dotnet tool install -g vpk" -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
