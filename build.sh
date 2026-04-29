#!/bin/bash
# Build script for the Continue Watching Deduplicator plugin

set -e

PLUGIN_NAME="Jellyfin.Plugin.ContinueWatchingDedup"
VERSION="1.0.0.0"

echo "🔨 Building $PLUGIN_NAME v$VERSION..."

# Clean previous builds
rm -rf "$PLUGIN_NAME/bin" "$PLUGIN_NAME/obj" "dist"

# Build & publish
dotnet publish "$PLUGIN_NAME/$PLUGIN_NAME.csproj" \
    -c Release \
    -o "dist/$PLUGIN_NAME" \
    --no-self-contained

# Package as zip for Jellyfin
cd dist
zip -r "${PLUGIN_NAME}_${VERSION}.zip" "$PLUGIN_NAME"
cd ..

echo ""
echo "✅ Build complete!"
echo "📦 Package: dist/${PLUGIN_NAME}_${VERSION}.zip"
echo ""
echo "To install:"
echo "  1. Copy dist/$PLUGIN_NAME/$PLUGIN_NAME.dll to your Jellyfin plugins folder"
echo "     - Linux: /var/lib/jellyfin/plugins/$PLUGIN_NAME/"
echo "     - Docker: /config/plugins/$PLUGIN_NAME/"
echo "     - Windows: %ProgramData%\\Jellyfin\\Server\\plugins\\$PLUGIN_NAME\\"
echo "  2. Restart Jellyfin"
