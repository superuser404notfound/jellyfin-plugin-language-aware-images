#!/usr/bin/env bash
# Builds the plugin in Release mode and copies the artifacts into the
# Jellyfin plugin directory.
#
# Override the install location with JELLYFIN_PLUGIN_DIR.
# Default is the macOS official-installer path.
set -euo pipefail

PLUGIN_DIR="${JELLYFIN_PLUGIN_DIR:-$HOME/Library/Application Support/jellyfin/plugins/LanguageAwareImages}"

cd "$(dirname "$0")"

dotnet publish -c Release -o ./bin/publish

mkdir -p "$PLUGIN_DIR"
cp ./bin/publish/Jellyfin.Plugin.LanguageAwareImages.dll "$PLUGIN_DIR/"
cp ./bin/publish/TMDbLib.dll "$PLUGIN_DIR/"
cp ./meta.json "$PLUGIN_DIR/"

echo
echo "Installed to: $PLUGIN_DIR"
echo "Restart Jellyfin and check Admin → Plugins."
