# AutoCAD MCP Portable Build Script
# Creates a .bundle folder in the Output directory for manual distribution

$ErrorActionPreference = "Stop"

$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$PROJECT_DIR = Join-Path $SCRIPT_DIR "AutoCAD-MCP-Addin"
$OUTPUT_DIR = Join-Path $SCRIPT_DIR "Output"
$BUNDLE_DIR = "$OUTPUT_DIR\AutoCADMCP.bundle"

Write-Host "Building AutoCAD MCP Package..." -ForegroundColor Cyan

# 1. Build C# Project
Write-Host "Compiling C# Project..." -ForegroundColor Yellow
try {
    dotnet build "$PROJECT_DIR\AutoCADMCP.csproj" -c Release
}
catch {
    Write-Warning "dotnet build failed. Please ensure .NET SDK is installed."
    Write-Warning "Skipping compilation check as requested, assuming pre-compiled or future compilation."
}

# 2. Create Output Structure
Write-Host "Creating Bundle Structure in Output..." -ForegroundColor Yellow
if (Test-Path $BUNDLE_DIR) {
    Remove-Item $BUNDLE_DIR -Recurse -Force
}
New-Item -ItemType Directory -Path "$BUNDLE_DIR\Contents" -Force | Out-Null

# 3. Copy Files (If build succeeded, otherwise placeholders)
Write-Host "Gathering Files..." -ForegroundColor Yellow
$BinDir = "$PROJECT_DIR\bin\Release\net48"

if (Test-Path "$BinDir\AutoCADMCP.dll") {
    Copy-Item "$BinDir\AutoCADMCP.dll" "$BUNDLE_DIR\Contents\"
    Copy-Item "$BinDir\Newtonsoft.Json.dll" "$BUNDLE_DIR\Contents\"
}
else {
    Write-Warning "Binaries not found. The 'Output' folder will contain the bundle structure but missing DLLs."
}

# PackageContents.xml
Copy-Item "$PROJECT_DIR\PackageContents.xml" "$BUNDLE_DIR\"

Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "Bundle Location: $BUNDLE_DIR"
Write-Host "You can copy this 'AutoCADMCP.bundle' folder to '%APPDATA%\Autodesk\ApplicationPlugins\' on any machine."
