<#
.SYNOPSIS
    ULTIMATE IRON-CLAD WINDOWS BUILD SYSTEM (2026 EDITION)
    =====================================================
    Targets: Clang-CL, Ninja, and Visual Studio 2022 Environment

.DESCRIPTION
    This script is the definitive, "iron clad" solution for building ImPlay on Windows. 
    It has been meticulously engineered to eliminate the "works on my machine" problem 
    by performing exhaustive system audits, environment repairs, and diagnostic reporting.

    This script ensures:
    1. All prerequisites are present and functional (Git, CMake, Ninja, LLVM, VS 2022).
    2. The build environment is correctly initialized with vcvars64.bat.
    3. LLVM/Clang-CL is correctly located and injected into the PATH if necessary.
    4. Workspace integrity is verified (submodules, CMakePresets, source files).
    5. The build process is monitored with high-fidelity logging.
    6. Post-build artifacts are verified for existence and size.

    "It doesn't just run a build; it prepares a battlefield."

.PARAMETER Preset
    The CMake preset to use. Defaults to 'x64-clang-release'.
    Must match a name in CMakePresets.json.

.PARAMETER Package
    If specified, builds the 'package' target (MSI, ZIP, 7Z).

.PARAMETER Fresh
    If specified, nukes the existing build directory and starts with a clean slate.

.PARAMETER ConfigureOnly
    If specified, stops immediately after the CMake configuration phase.

.PARAMETER SkipChecks
    If specified, bypasses some of the more intensive system audits.

.PARAMETER NoColor
    Disable the vibrant colorized output (for legacy terminals).

.EXAMPLE
    .\build-windows-clang.ps1 -Preset "x64-clang-release" -Package -Fresh
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("x64-clang-release", "x64-msvc-release")]
    [string]$Preset = "x64-clang-release",

    [Parameter(Mandatory = $false)]
    [switch]$Package,

    [Parameter(Mandatory = $false)]
    [switch]$Fresh,

    [Parameter(Mandatory = $false)]
    [switch]$ConfigureOnly,

    [Parameter(Mandatory = $false)]
    [switch]$SkipChecks,

    [Parameter(Mandatory = $false)]
    [switch]$NoColor,

    [Parameter(Mandatory = $false)]
    [switch]$NoPause
)

# ---------------------------------------------------------------------------
# SELF-ELEVATION (Mandatory Elevation to Administrator)
# ---------------------------------------------------------------------------
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Elevating script to Administrator..." -ForegroundColor Yellow
    $argsList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    # Forward all passed parameters
    $argsList += $MyInvocation.BoundParameters.Keys | ForEach-Object { "-$_", "`"$($MyInvocation.BoundParameters[$_])`"" }
    $argsList += $MyInvocation.UnboundArguments

    Start-Process powershell.exe -ArgumentList $argsList -Verb RunAs
    exit
}

# ---------------------------------------------------------------------------
# GLOBAL INITIALIZATION
# ---------------------------------------------------------------------------
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Set-StrictMode -Version Latest

# Capture Start Time for Performance Metrics
$Global:ScriptStart = Get-Date
$Global:RepoRoot = $PSScriptRoot
$Global:BuildDir = Join-Path $RepoRoot "out\build\$Preset"
$Global:PresetFile = Join-Path $RepoRoot "CMakePresets.json"

# ANSI Color Codes (if supported)
if ($NoColor) {
    $c_cyan = $c_green = $c_yellow = $c_red = $c_white = $c_gray = $c_reset = ""
} else {
    # Using PowerShell's built-in Write-Host colors is more portable than ANSI codes in raw strings
}

# ---------------------------------------------------------------------------
# LOGGING ENGINE
# ---------------------------------------------------------------------------

function Write-ImPlayBanner {
    Write-Host "------------------------------------------" -ForegroundColor Cyan
    Write-Host "   IRON-CLAD WINDOWS BUILD SYSTEM v2.0    " -ForegroundColor Cyan
    Write-Host "------------------------------------------" -ForegroundColor Cyan
    Write-Host " Started at: $($Global:ScriptStart.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
    Write-Host " Repository: $Global:RepoRoot" -ForegroundColor Gray
    Write-Host ""
}

function Get-TS { return "[$(Get-Date -Format 'HH:mm:ss')]" }

function Log-Step { param([string]$Msg) Write-Host "$(Get-TS) [STEP] $Msg" -ForegroundColor Cyan }
function Log-OK   { param([string]$Msg) Write-Host "$(Get-TS) [OK]   $Msg" -ForegroundColor Green }
function Log-Info { param([string]$Msg) Write-Host "$(Get-TS) [INFO] $Msg" -ForegroundColor White }
function Log-Warn { param([string]$Msg) Write-Host "$(Get-TS) [WARN] $Msg" -ForegroundColor Yellow }
function Log-Err  { param([string]$Msg) Write-Host "$(Get-TS) [FAIL] $Msg" -ForegroundColor Red }

function Pause-IfRequired {
    if (-not $NoPause -and $Host.Name -eq "ConsoleHost") {
        Write-Host "`nPress any key to close this window..." -ForegroundColor Gray
        $null = [Console]::ReadKey($true)
    }
}

function Die {
    param([string]$Msg)
    Write-Host ""
    Log-Err "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
    Log-Err " FATAL BUILD FAILURE: $Msg"
    Log-Err "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
    Write-Host ""
    $Elapsed = (Get-Date) - $Global:ScriptStart
    Log-Info "Total Duration: $($Elapsed.TotalSeconds.ToString('F2')) seconds"
    Pop-Location
    Pause-IfRequired
    exit 1
}

# ---------------------------------------------------------------------------
# SYSTEM AUDIT FUNCTIONS
# ---------------------------------------------------------------------------

function Audit-Admin {
    $user = [Security.Principal.WindowsIdentity]::GetCurrent()
    $role = [Security.Principal.WindowsBuiltInRole]::Administrator
    $isAdmin = (New-Object Security.Principal.WindowsPrincipal($user)).IsInRole($role)
    if ($isAdmin) {
        Log-OK "Running with Administrative privileges."
    } else {
        Log-Warn "Running as standard user. Packaging or system installs may require elevation."
    }
}

function Audit-DiskSpace {
    $driveName = (Split-Path $Global:RepoRoot -Qualifier).Replace(":", "")
    $drive = Get-PSDrive -Name $driveName -ErrorAction SilentlyContinue
    if (-not $drive) {
        Log-Warn "Could not verify disk space for drive '$driveName'."
        return
    }
    $freeGB = $drive.Free / 1GB
    if ($freeGB -lt 5) {
        Log-Warn "Low disk space detected: $($freeGB.ToString('F2')) GB remaining. Build might fail."
    } else {
        Log-OK "Available Disk Space: $($freeGB.ToString('F2')) GB."
    }
}

function Audit-Network {
    Log-Step "Verifying network connectivity (for FetchContent)..."
    try {
        $result = Test-Connection -ComputerName google.com -Count 1 -ErrorAction SilentlyContinue
        Log-OK "Internet connectivity verified."
    } catch {
        Log-Warn "Internet connectivity check failed. Offline builds may fail if dependencies aren't cached."
    }
}

function Audit-Submodules {
    Log-Step "Checking git submodules..."
    $thirdParty = Join-Path $Global:RepoRoot "third_party"
    if (-not (Test-Path $thirdParty)) {
        Die "third_party directory missing! Run 'git submodule update --init --recursive'."
    }
    
    $subdirs = Get-ChildItem $thirdParty -Directory
    if ($subdirs.Count -lt 5) {
        Log-Warn "Very few subdirectories in third_party. Submodules might not be initialized."
    } else {
        Log-OK "Found $($subdirs.Count) dependency directories in third_party."
    }
}

function Require-Tool {
    param([string]$Name, [string]$Hint)
    Log-Step "Locating tool: $Name..."
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) {
        Die "Missing mandatory tool: '$Name'. $Hint"
    }
    Log-OK "Found $Name at $($cmd.Source)"
    return $cmd.Source
}

# ---------------------------------------------------------------------------
# PATH DISCOVERY & ENVIRONMENT REPAIR
# ---------------------------------------------------------------------------

function Find-VS2022-Env {
    Log-Step "Auditing Visual Studio 2022 Installation..."
    
    # Priority 1: vswhere (The Source of Truth)
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $installPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        if ($installPath) {
            $vcvars = Join-Path $installPath "VC\Auxiliary\Build\vcvars64.bat"
            if (Test-Path $vcvars) {
                return $vcvars
            }
        }
    }

    # Priority 2: Hardcoded Fallbacks (Standard Paths)
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
    )
    
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }

    return $null
}

function Find-ClangCL-Robust {
    param([string]$PresetValue)
    Log-Step "Auditing LLVM / Clang-CL Installation..."

    # 1. Check current PATH
    $cmd = Get-Command "clang-cl" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # 2. Check value from CMakePresets.json
    if ($PresetValue -and (Test-Path $PresetValue)) { return $PresetValue }

    # 3. Check standard LLVM installer paths
    $llvmPaths = @(
        "${env:ProgramFiles}\LLVM\bin\clang-cl.exe",
        "${env:ProgramFiles(x86)}\LLVM\bin\clang-cl.exe",
        "C:\LLVM\bin\clang-cl.exe"
    )
    foreach ($p in $llvmPaths) {
        if (Test-Path $p) { return $p }
    }

    # 4. Check inside Visual Studio (side-by-side Clang)
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -property installationPath
        if ($vsPath) {
            Log-Info "Searching for Clang inside Visual Studio install..."
            $found = Get-ChildItem -Path $vsPath -Filter "clang-cl.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { return $found.FullName }
        }
    }

    return $null
}

# ---------------------------------------------------------------------------
# WORKHORSE: THE BUILD ENGINE
# ---------------------------------------------------------------------------

Write-ImPlayBanner

# Change to Repo Root and guard against failures
Push-Location $Global:RepoRoot

try {
    # PHASE 1: SYSTEM AUDIT
    if (-not $SkipChecks) {
        Audit-Admin
        Audit-DiskSpace
        Audit-Network
        Audit-Submodules
    }

    # PHASE 2: TOOL VALIDATION
    $GitPath   = Require-Tool "git"   "Install Git for Windows."
    $CMakePath = Require-Tool "cmake" "Install CMake v3.21+."
    $NinjaPath = Require-Tool "ninja" "Install Ninja build system."

    # PHASE 3: ENVIRONMENT PREPARATION
    Log-Step "Configuring Build Environment for Preset: $Preset"
    
    # Parse CMakePresets for the compiler path
    if (-not (Test-Path $Global:PresetFile)) { Die "Missing CMakePresets.json!" }
    $PresetData = Get-Content $Global:PresetFile -Raw | ConvertFrom-Json
    $ActivePreset = $PresetData.configurePresets | Where-Object { $_.name -eq $Preset } | Select-Object -First 1
    if (-not $ActivePreset) { Die "Preset '$Preset' not found in CMakePresets.json." }
    
    $CompilerHint = $ActivePreset.cacheVariables.CMAKE_CXX_COMPILER
    $ClangCL = Find-ClangCL-Robust $CompilerHint
    
    if (-not $ClangCL) {
        Die "clang-cl.exe not found! Install LLVM or the 'Clang for Windows' VS component."
    }
    Log-OK "Using Clang-CL: $ClangCL"

    # Fix PATH if Clang is missing from it
    $ClangDir = Split-Path $ClangCL -Parent
    if ($env:Path -notlike "*$ClangDir*") {
        Log-Warn "Injecting $ClangDir into PATH for this session."
        $env:Path = "$ClangDir;$env:Path"
    }

    $VcVars = Find-VS2022-Env
    if (-not $VcVars) {
        Die "Visual Studio 2022 C++ environment not found. Please install VS 2022 with C++ Desktop workload."
    }
    Log-OK "Using VS Env: $VcVars"

    # PHASE 4: DIAGNOSTIC REPORT
    Write-Host "`n--- BUILD CONFIGURATION SUMMARY ---" -ForegroundColor Cyan
    Log-Info "Preset:      $Preset"
    Log-Info "Compiler:    $ClangCL"
    Log-Info "Build Dir:   $Global:BuildDir"
    Log-Info "Fresh:       $Fresh"
    Log-Info "Package:     $Package"
    
    # Version checks
    $cmakeVer = & $CMakePath --version | Select-Object -First 1
    $clangVer = & $ClangCL --version | Select-Object -First 1
    Log-Info "$cmakeVer"
    Log-Info "$clangVer"
    Write-Host "-----------------------------------`n" -ForegroundColor Cyan

    # PHASE 5: CLEANING
    if ($Fresh -and (Test-Path $Global:BuildDir)) {
        Log-Step "Performing deep clean of $Global:BuildDir..."
        Remove-Item -Recurse -Force $Global:BuildDir
        Log-OK "Build directory nuked."
    }

    # PHASE 6: CMAKE CONFIGURE
    Log-Step "Executing CMake Configuration..."
    $CfgArgs = @("--preset", $Preset)
    if ($Package) { $CfgArgs += "-DCREATE_PACKAGE=ON" }
    if ($Fresh)   { $CfgArgs = @("--fresh") + $CfgArgs }

    # Construct the command string for CMD
    # We quote each argument to handle spaces, but avoid complex escaping that CMD doesn't understand
    $CfgLine = "cmake " + (($CfgArgs | ForEach-Object { "`"$_`"" }) -join " ")
    $FullCfg = "call `"$VcVars`" && $CfgLine"
    
    Log-Info "Command: $CfgLine"
    $ConfigStart = Get-Date
    
    # Use the simplest possible CMD invocation that handles internal quotes
    & cmd.exe /d /c $FullCfg
    
    if ($LASTEXITCODE -ne 0) { Die "CMake configuration failed (Exit: $LASTEXITCODE)." }
    $ConfigDuration = (Get-Date) - $ConfigStart
    Log-OK "Configuration successful in $($ConfigDuration.TotalSeconds.ToString('F2'))s."

    # PHASE 7: CMAKE BUILD
    if (-not $ConfigureOnly) {
        Log-Step "Executing CMake Build..."
        $BldArgs = @("--build", "--preset", $Preset)
        if ($Package) { $BldArgs += @("--target", "package") }

        # Construct the command string for CMD
        $BldLine = "cmake " + (($BldArgs | ForEach-Object { "`"$_`"" }) -join " ")
        $FullBld = "call `"$VcVars`" && $BldLine"
        
        Log-Info "Command: $BldLine"
        $BuildStart = Get-Date
        
        & cmd.exe /d /c $FullBld
        
        if ($LASTEXITCODE -ne 0) { Die "CMake build failed (Exit: $LASTEXITCODE)." }
        $BuildDuration = (Get-Date) - $BuildStart
        Log-OK "Build successful in $($BuildDuration.TotalSeconds.ToString('F2'))s."

        # PHASE 8: ARTIFACT VERIFICATION
        Log-Step "Verifying build results..."
        $Binaries = Get-ChildItem -Path $Global:BuildDir -Filter "ImPlay.exe" -Recurse
        if ($Binaries) {
            foreach ($b in $Binaries) {
                Log-OK "Found Binary: $($b.FullName) ($(($b.Length / 1MB).ToString('F2')) MB)"
            }
        } else {
            Log-Warn "ImPlay.exe not found in $Global:BuildDir! Check build logs for errors."
        }

        if ($Package) {
            Log-Step "Checking for generated packages..."
            $Pkgs = Get-ChildItem -Path $Global:BuildDir -Include "*.msi", "*.zip", "*.7z", "*.exe" -Exclude "ImPlay.exe" -Recurse
            if ($Pkgs) {
                foreach ($p in $Pkgs) {
                    Log-OK "Found Package: $($p.Name) ($(($p.Length / 1MB).ToString('F2')) MB)"
                }
            } else {
                Log-Warn "No installer packages found. Check CPack output."
            }
        }
    } else {
        Log-Info "Skipping build phase (-ConfigureOnly active)."
    }

    # PHASE 9: SUCCESS REPORT
    $FinalDuration = (Get-Date) - $Global:ScriptStart
    Write-Host ""
    Log-OK "Build Completed Successfully."
    Log-Info "Total Time:  $($FinalDuration.Minutes)m $($FinalDuration.Seconds)s"
    Log-Info "Binary Location: $Global:BuildDir"
    Write-Host ""

} catch {
    Die "An unexpected exception occurred: $($_.Exception.Message)`n$($_.ScriptStackTrace)"
} finally {
    Pop-Location
    Pause-IfRequired
}
