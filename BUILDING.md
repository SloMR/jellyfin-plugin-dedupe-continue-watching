# Building the Plugin (All Platforms)

The same `.dll` works on every Jellyfin server regardless of where you build it. Pick the OS you're most comfortable with.

---

## Prerequisites (All OS)

You need **.NET 8 SDK** installed.

### Linux (Ubuntu/Debian)

```bash
sudo apt update
sudo apt install -y dotnet-sdk-8.0
```

### Linux (Arch / Manjaro)

```bash
sudo pacman -S dotnet-sdk
```

### Linux (Fedora / RHEL)

```bash
sudo dnf install dotnet-sdk-8.0
```

### macOS

**Option A — Homebrew (recommended):**
```bash
brew install dotnet@8
```

**Option B — Official installer:**
Download from https://dotnet.microsoft.com/download/dotnet/8.0 — pick the macOS installer matching your CPU (Intel `x64` or Apple Silicon `arm64`).

### Windows

**Option A — winget:**
```powershell
winget install Microsoft.DotNet.SDK.8
```

**Option B — Official installer:**
Download from https://dotnet.microsoft.com/download/dotnet/8.0 — pick "SDK" for x64.

**Option C — Chocolatey:**
```powershell
choco install dotnet-8.0-sdk
```

Verify with:
```bash
dotnet --version
```
Should print `8.0.x`.

---

## Building

> **Just want to install it?** Don't build from source — grab the latest `.zip` from [Releases](https://github.com/SloMR/jellyfin-plugin-dedupe-continue-watching/releases) (built by CI on every tag) or add the plugin repository (see the [README](README.md#installation)). Build from source only if you're modifying the plugin or want to verify the artifact.

### Linux / macOS

```bash
git clone https://github.com/SloMR/jellyfin-plugin-dedupe-continue-watching.git
cd jellyfin-plugin-dedupe-continue-watching
chmod +x build.sh
./build.sh
```

### Windows (PowerShell)

```powershell
git clone https://github.com/SloMR/jellyfin-plugin-dedupe-continue-watching.git
cd jellyfin-plugin-dedupe-continue-watching
.\build.ps1
```

### Manual (Any OS)

If the scripts don't work:

```bash
cd jellyfin-plugin-dedupe-continue-watching
dotnet publish Jellyfin.Plugin.ContinueWatchingDedup/Jellyfin.Plugin.ContinueWatchingDedup.csproj -c Release -o dist
```

The `.dll` will be at:
```
dist/Jellyfin.Plugin.ContinueWatchingDedup.dll
```

---

## Installing on Your Jellyfin Server

### Linux (native install)

```bash
sudo systemctl stop jellyfin
sudo mkdir -p /var/lib/jellyfin/plugins/Jellyfin.Plugin.ContinueWatchingDedup_1.0.0.0
sudo cp dist/Jellyfin.Plugin.ContinueWatchingDedup.dll \
        /var/lib/jellyfin/plugins/Jellyfin.Plugin.ContinueWatchingDedup_1.0.0.0/
sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/
sudo systemctl start jellyfin
```

### Linux (Docker)

```bash
docker stop jellyfin
mkdir -p /your/jellyfin/config/plugins/Jellyfin.Plugin.ContinueWatchingDedup_1.0.0.0
cp dist/Jellyfin.Plugin.ContinueWatchingDedup.dll \
   /your/jellyfin/config/plugins/Jellyfin.Plugin.ContinueWatchingDedup_1.0.0.0/
docker start jellyfin
```

### macOS

```bash
# Stop Jellyfin app
osascript -e 'quit app "Jellyfin"'

# Copy plugin
mkdir -p ~/.local/share/jellyfin/plugins/Jellyfin.Plugin.ContinueWatchingDedup_1.0.0.0
cp dist/Jellyfin.Plugin.ContinueWatchingDedup.dll \
   ~/.local/share/jellyfin/plugins/Jellyfin.Plugin.ContinueWatchingDedup_1.0.0.0/

# Restart Jellyfin
open -a Jellyfin
```

### Windows

```powershell
# Stop service
Stop-Service Jellyfin

# Create plugin folder
$pluginPath = "$env:ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.ContinueWatchingDedup_1.0.0.0"
New-Item -ItemType Directory -Force -Path $pluginPath

# Copy DLL
Copy-Item "dist\Jellyfin.Plugin.ContinueWatchingDedup\Jellyfin.Plugin.ContinueWatchingDedup.dll" `
          -Destination $pluginPath

# Start service
Start-Service Jellyfin
```

---

## Verifying the Install

1. Open Jellyfin web UI
2. Go to **Dashboard → Plugins → My Plugins**
3. You should see **Continue Watching Deduplicator** listed
4. Click it to access the settings page
5. Open Continue Watching on any client — duplicates should be gone

---

## Troubleshooting

### Plugin doesn't appear after restart

- Check Jellyfin logs: `sudo tail -f /var/log/jellyfin/jellyfin$(date +%Y%m%d).log` (Linux native package) or `%ProgramData%\Jellyfin\Server\log\` (Windows). Docker users: `docker logs -f jellyfin`.
- Look for lines mentioning `ContinueWatchingDedup` or plugin loading errors
- Verify the folder is owned by the jellyfin user (`chown jellyfin:jellyfin`)

### "Plugin failed to load"

Usually means a `.NET` version mismatch. Make sure:
- You built with .NET 8 SDK
- Your Jellyfin server is version **10.10.0 or newer**

### Build error: "Could not find package Jellyfin.Controller"

Run:
```bash
dotnet restore
```
This downloads NuGet dependencies. Then rebuild.
