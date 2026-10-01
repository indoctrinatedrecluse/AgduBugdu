<#
.SYNOPSIS
    Builds, repairs dependencies, and runs AgduBugdu editor.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.PARAMETER LaunchApp
    Launches the AgduBugdu.App executable after successful build.
.PARAMETER RunTests
    Executes unit test suite after successful build.
.PARAMETER ForceRestore
    Forces a fresh package restore ignoring caches.
.PARAMETER VerboseLogging
    Enables detailed output for dotnet commands.
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$LaunchApp,
    [switch]$RunTests,
    [switch]$ForceRestore,
    [switch]$VerboseLogging
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " AgduBugdu Build & Dependency Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Check .NET SDK prerequisite
Write-Host "`n[1/4] Checking .NET environment..." -ForegroundColor Yellow
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "ERROR: .NET SDK is not installed or not in PATH. Please install .NET SDK from https://dotnet.microsoft.com/"
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "Found .NET SDK: $dotnetVersion" -ForegroundColor Green

# 2. Check and repair nuget.config
Write-Host "`n[2/4] Validating package sources & dependencies..." -ForegroundColor Yellow
$nugetConfigFile = Join-Path $RepoRoot "nuget.config"
if (-not (Test-Path $nugetConfigFile)) {
    Write-Host "Creating missing nuget.config with nuget.org package source..." -ForegroundColor Magenta
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="Microsoft Visual Studio Offline Packages" value="C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\" />
  </packageSources>
</configuration>
"@ | Out-File -FilePath $nugetConfigFile -Encoding utf8
}

$solutionFile = Join-Path $RepoRoot "AgduBugdu.slnx"
if (-not (Test-Path $solutionFile)) {
    $solutionFile = Join-Path $RepoRoot "AgduBugdu.sln"
}

$verbosity = if ($VerboseLogging) { "normal" } else { "minimal" }

# 3. Restore dependencies (with repair fallback)
Write-Host "Restoring NuGet dependencies for $(Split-Path $solutionFile -Leaf)..." -ForegroundColor Cyan
if ($ForceRestore) {
    dotnet restore $solutionFile --force --no-cache -v $verbosity
} else {
    dotnet restore $solutionFile -v $verbosity
}

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Initial restore reported issues. Attempting dependency repair with --force..."
    dotnet restore $solutionFile --force -v normal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore NuGet packages. Check network connection and package sources."
        exit $LASTEXITCODE
    }
}
Write-Host "Dependencies verified and restored successfully." -ForegroundColor Green

# 4. Build solution
Write-Host "`n[3/4] Building solution ($Configuration)..." -ForegroundColor Yellow
dotnet build $solutionFile -c $Configuration --no-restore -v $verbosity

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
Write-Host "Build succeeded cleanly!" -ForegroundColor Green

# 5. Optional tests
if ($RunTests) {
    Write-Host "`n[Optional] Running test suite..." -ForegroundColor Yellow
    dotnet test $solutionFile -c $Configuration --no-build -v normal --logger "console;verbosity=detailed"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unit tests failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Host "All tests passed!" -ForegroundColor Green
}

# 6. Optional launch
if ($LaunchApp) {
    Write-Host "`n[4/4] Launching AgduBugdu.App..." -ForegroundColor Cyan
    $appExe = Join-Path $RepoRoot "src\AgduBugdu.App\bin\$Configuration\net8.0\AgduBugdu.App.exe"
    if (Test-Path $appExe) {
        Write-Host "Starting GUI process: $appExe" -ForegroundColor Green
        Start-Process -FilePath $appExe -WorkingDirectory (Join-Path $RepoRoot "src\AgduBugdu.App")
        Write-Host "AgduBugdu window launched successfully!" -ForegroundColor Green
    } else {
        $appCsproj = Join-Path $RepoRoot "src\AgduBugdu.App\AgduBugdu.App.csproj"
        dotnet run --project $appCsproj -c $Configuration --no-build
    }
} else {
    Write-Host "`nBuild complete. To launch the editor, pass -LaunchApp or run: dotnet run --project src/AgduBugdu.App" -ForegroundColor Gray
}
