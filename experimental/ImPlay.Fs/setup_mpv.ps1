$url = "https://downloads.sourceforge.net/mpv-player-windows/mpv-dev-x86_64-20260503-git-948c86d24c.7z"
$output = "mpv-dev.7z"
$extractDir = "mpv-dev"

Write-Host "Downloading libmpv..."
Invoke-WebRequest -Uri $url -OutFile $output

if (Test-Path $extractDir) { Remove-Item -Recurse -Force $extractDir }
New-Item -ItemType Directory -Path $extractDir

Write-Host "Extracting libmpv..."
7z x $output -o$extractDir -y

$binDirs = @(
    "bin/Debug/net10.0",
    "bin/Release/net10.0"
)

foreach ($dir in $binDirs) {
    if (Test-Path $dir) {
        Write-Host "Copying libmpv-2.dll to $dir..."
        Copy-Item "$extractDir/libmpv-2.dll" "$dir/" -Force
    }
}

Write-Host "Done."
