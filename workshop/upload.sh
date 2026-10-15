#!/usr/bin/env bash
# Uploads the built mod to the Steam Workshop with SteamCMD, together with a
# change note for the "Change Notes" tab. Title, description, preview image and
# tags stay as they are on the Workshop page.
#
# Usage (Git Bash, from the mod's folder, after "dotnet build"):
#   bash workshop/upload.sh "1.0.2: What changed."
#
# Needs ../workshop.local.sh next to the mod folders (not in git):
#   STEAM_LOGIN="your Steam login name"
#   STEAMCMD="/g/dev/steamcmd/steamcmd.exe"

set -e

NOTE="$1"
if [ -z "$NOTE" ]; then
  echo 'Usage: bash workshop/upload.sh "1.0.2: What changed."'
  exit 1
fi

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOCAL="$ROOT/../workshop.local.sh"
if [ ! -f "$LOCAL" ]; then
  echo "Not found: $LOCAL"
  echo "Create it with STEAM_LOGIN=\"...\" and STEAMCMD=\"/g/dev/steamcmd/steamcmd.exe\"."
  exit 1
fi
source "$LOCAL"

ITEM_ID="$(tr -d '[:space:]' < "$ROOT/workshop/publishedfileid.txt")"

# The built mod: the same folder the .csproj installs into
DOCS="$(powershell -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')" | tr -d '\r')"
MOD_DIR="$(cygpath -m "$DOCS")/Klei/OxygenNotIncluded/mods/Local/ResourceSensor"
if [ ! -f "$MOD_DIR/mod_info.yaml" ]; then
  echo "No built mod in $MOD_DIR. Run \"dotnet build\" first."
  exit 1
fi
VERSION="$(grep -m1 '^version:' "$MOD_DIR/mod_info.yaml" | sed 's/version:[[:space:]]*//' | tr -d '\r')"

echo "Workshop item: $ITEM_ID"
echo "Mod folder:    $MOD_DIR"
echo "Version:       $VERSION"
echo "Change note:   $NOTE"
read -r -p "Upload? (y/n) " ANSWER
if [ "$ANSWER" != "y" ]; then
  echo "Cancelled."
  exit 0
fi

# The upload description for SteamCMD, written to a temporary file
VDF="$(mktemp --suffix=.vdf)"
NOTE_ESCAPED="$(printf '%s' "$NOTE" | sed 's/\\/\\\\/g; s/"/\\"/g')"
cat > "$VDF" <<VDFEOF
"workshopitem"
{
  "appid" "457140"
  "publishedfileid" "$ITEM_ID"
  "contentfolder" "$MOD_DIR"
  "changenote" "$NOTE_ESCAPED"
}
VDFEOF

"$STEAMCMD" +login "$STEAM_LOGIN" +workshop_build_item "$(cygpath -m "$VDF")" +quit
rm -f "$VDF"
