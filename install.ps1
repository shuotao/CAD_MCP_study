# AutoCAD MCP Deployment Script
# Compiles the C# project and installs it to the user's AutoCAD ApplicationPlugins folder

$ErrorActionPreference = "Stop"

$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$PROJECT_DIR = Join-Path $SCRIPT_DIR "AutoCAD-MCP-Addin"
$TARGET_DIR = "$env:APPDATA\Autodesk\ApplicationPlugins\AutoCADMCP.bundle"

Write-Host "Build and Deploy AutoCAD MCP..." -ForegroundColor Cyan

# 1. Build C# Project
Write-Host "Building C# Project..." -ForegroundColor Yellow
$msbuild = "dotnet build '$PROJECT_DIR\AutoCADMCP.csproj' -c Release"
Invoke-Expression $msbuild

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# 2. Create Bundle Structure
Write-Host "Creating Bundle Structure..." -ForegroundColor Yellow
if (Test-Path $TARGET_DIR) {
    Remove-Item $TARGET_DIR -Recurse -Force
}
New-Item -ItemType Directory -Path "$TARGET_DIR\Contents" -Force | Out-Null

# 3. Copy Files
Write-Host "Copying Files..." -ForegroundColor Yellow
$BinDir = "$PROJECT_DIR\bin\Release\net48"

# DLLs
Copy-Item "$BinDir\AutoCADMCP.dll" "$TARGET_DIR\Contents\"
Copy-Item "$BinDir\Newtonsoft.Json.dll" "$TARGET_DIR\Contents\"

# PackageContents.xml
Copy-Item "$PROJECT_DIR\PackageContents.xml" "$TARGET_DIR\"

Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "Folder: $TARGET_DIR"
Write-Host "Please restart AutoCAD to load the plugin."
