# Build script for Continue Watching Deduplicator plugin (Windows)
# Run with: .\build.ps1

$ErrorActionPreference = "Stop"

$PluginName = "Jellyfin.Plugin.ContinueWatchingDedup"
$Version = "1.0.0.0"

Write-Host "Building $PluginName v$Version..." -ForegroundColor Cyan

# Clean previous builds
if (Test-Path "$PluginName\bin") { Remove-Item -Recurse -Force "$PluginName\bin" }
if (Test-Path "$PluginName\obj") { Remove-Item -Recurse -Force "$PluginName\obj" }
if (Test-Path "dist") { Remove-Item -Recurse -Force "dist" }

# Build & publish
dotnet publish "$PluginName\$PluginName.csproj" `
    -c Release `
    -o "dist\$PluginName" `
    --no-self-contained

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Package as zip
$ZipPath = "dist\${PluginName}_${Version}.zip"
Compress-Archive -Path "dist\$PluginName\*" -DestinationPath $ZipPath -Force

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Package: $ZipPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "To install on Windows:"
Write-Host "  1. Stop Jellyfin service:  Stop-Service Jellyfin"
Write-Host "  2. Create folder:  `$env:ProgramData\Jellyfin\Server\plugins\${PluginName}_${Version}\"
Write-Host "  3. Copy DLL into that folder:"
Write-Host "     Copy-Item dist\$PluginName\$PluginName.dll -Destination `"`$env:ProgramData\Jellyfin\Server\plugins\${PluginName}_${Version}\`""
Write-Host "  4. Start Jellyfin service:  Start-Service Jellyfin"
