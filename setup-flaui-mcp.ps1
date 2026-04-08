<#
.SYNOPSIS
    Sets up FlaUI-MCP for use with Claude Code and the XrmToolBox Test Harness.

.DESCRIPTION
    Clones FlaUI-MCP, builds it, publishes to a local tools directory,
    and registers it as an MCP server in Claude Code.

.PARAMETER InstallDir
    Directory to publish FlaUI-MCP into. Default: C:\tools\FlaUI-MCP

.PARAMETER Scope
    Claude Code MCP scope: user (global) or project (this repo only). Default: user

.EXAMPLE
    .\setup-flaui-mcp.ps1
    .\setup-flaui-mcp.ps1 -InstallDir "D:\my-tools\FlaUI-MCP" -Scope project
#>
param(
    [string]$InstallDir = "C:\tools\FlaUI-MCP",
    [ValidateSet("user", "project")]
    [string]$Scope = "user"
)

$ErrorActionPreference = "Stop"

Write-Host "=== FlaUI-MCP Setup for XrmToolBox Test Harness ===" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

$dotnetVersion = & dotnet --version 2>$null
if (-not $dotnetVersion) {
    Write-Error ".NET SDK is required but not found. Install from https://dotnet.microsoft.com/download"
    exit 1
}
$major = [int]($dotnetVersion.Split('.')[0])
if ($major -lt 8) {
    Write-Error ".NET 8.0+ SDK is required (found $dotnetVersion). Install from https://dotnet.microsoft.com/download"
    exit 1
}
Write-Host "  .NET SDK: $dotnetVersion" -ForegroundColor Green

$git = & git --version 2>$null
if (-not $git) {
    Write-Error "Git is required but not found."
    exit 1
}
Write-Host "  Git: OK" -ForegroundColor Green

$claude = & claude --version 2>$null
if (-not $claude) {
    Write-Warning "Claude Code CLI not found. MCP registration will be skipped."
    Write-Warning "You can register manually later: claude mcp add flaui-mcp `"$InstallDir\FlaUI.Mcp.exe`""
    $skipClaude = $true
}
else {
    Write-Host "  Claude Code: OK" -ForegroundColor Green
    $skipClaude = $false
}

Write-Host ""

# Clone FlaUI-MCP to temp dir
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "flaui-mcp-setup-$([guid]::NewGuid().ToString('N').Substring(0,8))"
Write-Host "Cloning FlaUI-MCP..." -ForegroundColor Yellow
git clone --depth 1 https://github.com/shanselman/FlaUI-MCP.git $tempDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to clone FlaUI-MCP"
    exit 1
}
Write-Host "  Cloned to: $tempDir" -ForegroundColor Green

# Build and publish
Write-Host "Building and publishing to $InstallDir..." -ForegroundColor Yellow
dotnet publish "$tempDir\src\FlaUI.Mcp" -c Release -o $InstallDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
    exit 1
}
Write-Host "  Published to: $InstallDir" -ForegroundColor Green

# Cleanup temp
Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue

# Verify exe exists
$exePath = Join-Path $InstallDir "FlaUI.Mcp.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Expected $exePath but not found. Check build output."
    exit 1
}

# Register with Claude Code
if (-not $skipClaude) {
    Write-Host "Registering FlaUI-MCP with Claude Code (scope: $Scope)..." -ForegroundColor Yellow
    claude mcp add flaui-mcp $exePath -s $Scope
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Registered as MCP server 'flaui-mcp'" -ForegroundColor Green
    }
    else {
        Write-Warning "Auto-registration failed. Register manually:"
        Write-Warning "  claude mcp add flaui-mcp `"$exePath`""
    }
}

Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "FlaUI-MCP is installed at: $exePath" -ForegroundColor White
Write-Host ""
Write-Host "Usage:" -ForegroundColor Yellow
Write-Host "  1. Start the test harness with your plugin:" -ForegroundColor White
Write-Host "     .\src\XrmToolBox.TestHarness\bin\Release\net48\XrmToolBox.TestHarness.exe --plugin `"path\to\Plugin.dll`" --mockdata `"samples\basic-mockdata.json`"" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Claude Code can now drive the UI via the 'flaui-mcp' MCP server" -ForegroundColor White
