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

# Locate FlaUI-MCP source (submodule or clone)
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $repoRoot) { $repoRoot = $PSScriptRoot }
$submodulePath = Join-Path $PSScriptRoot "lib\FlaUI-MCP"

if (Test-Path (Join-Path $submodulePath "src\FlaUI.Mcp\FlaUI.Mcp.csproj")) {
    Write-Host "Using FlaUI-MCP from submodule at lib/FlaUI-MCP..." -ForegroundColor Yellow
    $sourcePath = $submodulePath
    $cleanupTemp = $false
} else {
    # Submodule not initialized — try to init it
    Write-Host "Initializing FlaUI-MCP submodule..." -ForegroundColor Yellow
    Push-Location $PSScriptRoot
    git submodule update --init lib/FlaUI-MCP 2>$null
    Pop-Location

    if (Test-Path (Join-Path $submodulePath "src\FlaUI.Mcp\FlaUI.Mcp.csproj")) {
        Write-Host "  Submodule initialized." -ForegroundColor Green
        $sourcePath = $submodulePath
        $cleanupTemp = $false
    } else {
        # Fallback: clone to temp dir
        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "flaui-mcp-setup-$([guid]::NewGuid().ToString('N').Substring(0,8))"
        Write-Host "Submodule not available. Cloning FlaUI-MCP from fork..." -ForegroundColor Yellow
        git clone --depth 1 https://github.com/HurleySk/FlaUI-MCP.git $tempDir
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to clone FlaUI-MCP"
            exit 1
        }
        Write-Host "  Cloned to: $tempDir" -ForegroundColor Green
        $sourcePath = $tempDir
        $cleanupTemp = $true
    }
}

# Stop running FlaUI-MCP process if it holds a lock on the install dir
$running = Get-Process -Name "FlaUI.Mcp" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running FlaUI-MCP process (PID $($running.Id)) to release file locks..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
    Write-Host "  Stopped." -ForegroundColor Green
    $wasRunning = $true
} else {
    $wasRunning = $false
}

# Build and publish
Write-Host "Building and publishing to $InstallDir..." -ForegroundColor Yellow
dotnet publish "$sourcePath\src\FlaUI.Mcp" -c Release -o $InstallDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    if ($cleanupTemp) { Remove-Item -Recurse -Force $sourcePath -ErrorAction SilentlyContinue }
    exit 1
}
Write-Host "  Published to: $InstallDir" -ForegroundColor Green

# Stamp version file so the skill can verify tool availability
$commitHash = git -C "$sourcePath" rev-parse --short HEAD 2>$null
$versionInfo = @{
    commit    = if ($commitHash) { $commitHash } else { "unknown" }
    builtAt   = (Get-Date -Format "o")
    tools     = @("windows_file_dialog", "windows_wait_for_element", "windows_find_elements", "windows_get_table_data")
}
$versionInfo | ConvertTo-Json | Set-Content (Join-Path $InstallDir "version.json") -Encoding UTF8
Write-Host "  Version stamp written to: $InstallDir\version.json" -ForegroundColor Green

# Cleanup temp if we cloned
if ($cleanupTemp) {
    Remove-Item -Recurse -Force $sourcePath -ErrorAction SilentlyContinue
}

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
if ($wasRunning) {
    Write-Host "IMPORTANT: FlaUI-MCP was stopped during the update." -ForegroundColor Red
    Write-Host "  Restart Claude Code (or start a new conversation) so the MCP server relaunches with the new binary." -ForegroundColor Yellow
    Write-Host ""
}
Write-Host "Usage:" -ForegroundColor Yellow
Write-Host "  1. Start the test harness with your plugin:" -ForegroundColor White
Write-Host "     .\src\XrmToolBox.TestHarness\bin\Release\net48\XrmToolBox.TestHarness.exe --plugin `"path\to\Plugin.dll`" --mockdata `"samples\basic-mockdata.json`"" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Claude Code can now drive the UI via the 'flaui-mcp' MCP server" -ForegroundColor White
