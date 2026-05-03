param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Resolve-Path (Join-Path $scriptDir "..")
$exePath = Join-Path $projectDir "bin\$Configuration\net10.0\ImPlay.Fs.exe"
$portableConfig = Join-Path (Split-Path -Parent $exePath) "portable_config"
$startupLog = Join-Path $portableConfig "startup.log"

if (-not (Test-Path $exePath)) {
    Write-Error "ImPlay.Fs.exe was not found at '$exePath'. Build the $Configuration configuration first."
}

if (-not (Test-Path $portableConfig)) {
    New-Item -ItemType Directory -Path $portableConfig | Out-Null
}

if (Test-Path $startupLog) {
    Remove-Item -LiteralPath $startupLog -Force
}

$nativeSource = @"
using System;
using System.Runtime.InteropServices;

public static class SmokeNative {
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
"@

if (-not ("SmokeNative" -as [type])) {
    Add-Type -TypeDefinition $nativeSource
}

function Write-Diagnostics {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Reason
    )

    Write-Host "Smoke launch failed: $Reason"
    Write-Host "Exe: $exePath"

    if ($null -ne $Process) {
        try {
            $Process.Refresh()
            Write-Host "Process: Id=$($Process.Id) HasExited=$($Process.HasExited) ExitCode=$(if ($Process.HasExited) { $Process.ExitCode } else { '<running>' }) MainWindowHandle=$($Process.MainWindowHandle) MainWindowTitle='$($Process.MainWindowTitle)'"
        } catch {
            Write-Host "Process info unavailable: $($_.Exception.Message)"
        }
    }

    if (Test-Path $startupLog) {
        Write-Host "--- startup.log tail ---"
        Get-Content -LiteralPath $startupLog -Tail 80 | ForEach-Object { Write-Host $_ }
        Write-Host "--- end startup.log tail ---"
    } else {
        Write-Host "No startup log was written at '$startupLog'."
    }

    Write-Host "--- recent ImPlay processes ---"
    Get-Process -Name "ImPlay.Fs" -ErrorAction SilentlyContinue |
        Select-Object Id, ProcessName, MainWindowHandle, MainWindowTitle, StartTime |
        Format-List |
        Out-String |
        Write-Host
    Write-Host "--- end recent ImPlay processes ---"
}

$process = $null
$passed = $false

try {
    Write-Host "Starting smoke launch: $exePath"
    $process = Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath) -PassThru

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()

        if ($process.HasExited) {
            Write-Diagnostics -Process $process -Reason "process exited before showing a window"
            exit 1
        }

        if ($process.MainWindowHandle -ne [IntPtr]::Zero -and
            [SmokeNative]::IsWindowVisible($process.MainWindowHandle) -and
            $process.MainWindowTitle -like "ImPlay Modern*") {
            $passed = $true
            break
        }
    }

    if (-not $passed) {
        Write-Diagnostics -Process $process -Reason "no visible ImPlay window appeared within $TimeoutSeconds seconds"
        exit 1
    }

    Write-Host "Smoke launch passed: visible window '$($process.MainWindowTitle)' appeared for process $($process.Id)."
    exit 0
}
finally {
    if ($null -ne $process) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
                Write-Host "Stopped smoke-test process $($process.Id)."
            }
        } catch {
            Write-Host "Could not stop smoke-test process: $($_.Exception.Message)"
        }
    }
}
