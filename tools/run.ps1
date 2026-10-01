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
  </packageSources>
</configuration>
"@ | Out-File -FilePath $nugetConfigFile -Encoding utf8
}