#!/usr/bin/env bash
# Builds the plugin in Release mode and copies the artifacts into the
# Jellyfin plugin directory.
#
# Pick the server line with TFM:
#   TFM=net10.0  (default)  Jellyfin 12
#   TFM=net8.0              Jellyfin 10.10 / 10.11
#
# Override the install location with JELLYFIN_PLUGIN_DIR.
# Default is the macOS official-installer path.
set -euo pipefail

TFM="${TFM:-net10.0}"
PLUGIN_DIR="${JELLYFIN_PLUGIN_DIR:-$HOME/Library/Application Support/jellyfin/plugins/LanguageAwareImages}"

cd "$(dirname "$0")"

command -v jq >/dev/null || { echo "jq is required (brew install jq)" >&2; exit 1; }

# The csproj owns the version and targetAbi for each server line, so read them
# back from MSBuild rather than duplicating the mapping here.
PROPS="$(dotnet msbuild -getProperty:AssemblyVersion -getProperty:PluginTargetAbi \
           -p:TargetFramework="$TFM" -nologo)"
VERSION="$(jq -er .Properties.AssemblyVersion <<< "$PROPS")"
ABI="$(jq -er .Properties.PluginTargetAbi <<< "$PROPS")"

dotnet publish -c Release -f "$TFM" -o "./bin/publish/$TFM"

mkdir -p "$PLUGIN_DIR"
cp "./bin/publish/$TFM/Jellyfin.Plugin.LanguageAwareImages.dll" "$PLUGIN_DIR/"
cp "./bin/publish/$TFM/TMDbLib.dll" "$PLUGIN_DIR/"
jq --arg v "$VERSION" --arg abi "$ABI" --arg fw "$TFM" \
   '.version = $v | .targetAbi = $abi | .framework = $fw' \
   meta.json > "$PLUGIN_DIR/meta.json"

echo
echo "Installed $VERSION ($TFM, targetAbi $ABI) to: $PLUGIN_DIR"
echo "Restart Jellyfin and check Admin → Plugins."
