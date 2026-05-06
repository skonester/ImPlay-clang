# ImPlay F# Build Script
# Publishes the F# Avalonia port into a single-file executable.

$ErrorActionPreference = "Stop"

$root = Get-Item $PSScriptRoot
$publishDir = Join-Path $root.FullName "publish"
$appProj = Join-Path $root.FullName "App\ImPlay.App.fsproj"

$lockingProcesses = Get-Process -Name "ImPlay" -ErrorAction SilentlyContinue
if ($lockingProcesses) {
    Write-Host "Stopping running ImPlay processes to prevent file locks..." -ForegroundColor DarkYellow
    $lockingProcesses | Stop-Process -Force
}

if (Test-Path $publishDir) {
    Write-Host "Clearing previous build artifacts..." -ForegroundColor Gray
    Remove-Item -Recurse $publishDir -Force
}

Write-Host "--- Restoring and Publishing ImPlay (F#) ---" -ForegroundColor Cyan

dotnet publish $appProj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Dotnet publish failed."
}

$libMpvCandidates = @(
    (Join-Path $publishDir "libmpv-2.dll"),
    (Join-Path $root.FullName "App\bin\Release\net10.0\win-x64\publish\libmpv-2.dll"),
    (Join-Path $root.FullName "App\bin\Release\net10.0\libmpv-2.dll"),
    (Join-Path $root.FullName "App\bin\Debug\net10.0\libmpv-2.dll"),
    (Join-Path $root.FullName "Core\bin\Release\net10.0\libmpv-2.dll")
)

$libMpvPath = $libMpvCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (Test-Path $libMpvPath) {
    Write-Host "Ensuring libmpv-2.dll is present in publish directory..."
    Copy-Item $libMpvPath $publishDir -Force
} else {
    Write-Warning "libmpv-2.dll was not found in expected locations. ImPlay may fail to start without it."
}

Write-Host "`nBuild complete! Output located in: $publishDir" -ForegroundColor Green
Write-Host "Run with: .\publish\ImPlay.exe"