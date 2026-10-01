<#
.SYNOPSIS
    Builds, tests, and publishes AgduBugdu for GitHub Actions CI/CD workflows.
.PARAMETER Rid
    Runtime identifier (win-x64, linux-x64, osx-x64, osx-arm64).
.PARAMETER Configuration
    Build configuration (default: Release).
.PARAMETER RunTests
    Executes unit test suite before publishing.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,

    [string]$Configuration = "Release",
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " AgduBugdu GitHub Actions CI Runner" -ForegroundColor Cyan
Write-Host " Target RID: $Rid | Config: $Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Restore only from official NuGet source for CI
Write-Host "`n[CI 1/3] Restoring solution..." -ForegroundColor Yellow
dotnet restore AgduBugdu.slnx --source "https://api.nuget.org/v3/index.json"

if ($LASTEXITCODE -ne 0) {
    Write-Error "CI restore failed with code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# 2. Run unit tests if requested
if ($RunTests) {
    Write-Host "`n[CI 2/3] Running test suite ($Configuration)..." -ForegroundColor Yellow
    dotnet test AgduBugdu.slnx -c $Configuration --no-restore --logger "console;verbosity=detailed"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "CI tests failed with code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}

# 3. Publish binary bundle
Write-Host "`n[CI 3/3] Publishing binaries for $Rid..." -ForegroundColor Yellow
$outDir = Join-Path $RepoRoot "publish\$Rid"
dotnet publish src/AgduBugdu.App/AgduBugdu.App.csproj `
    -c $Configuration `
    -r $Rid `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "CI publish failed with code $LASTEXITCODE."
    exit $LASTEXITCODE
}

Write-Host "`nCI Build and Publish succeeded for $Rid!" -ForegroundColor Green
