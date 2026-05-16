#!/bin/bash
set -euo pipefail

VERSION=$(grep -Po '(?<=<modVersion>).*?(?=</modVersion>)' About/About.xml | head -n 1)
if [ -z "$VERSION" ]; then
  echo "Could not read <modVersion> from About/About.xml"
  exit 1
fi

dotnet build Source/DisableSteamMods/DisableSteamMods.csproj --configuration Release || {
  echo "dotnet build FAILED"
  exit 1
}

OUT_ROOT="Workshop"
PACKAGE_DIR="$OUT_ROOT/DisableSteamMods"
ZIP_NAME="DisableSteamMods-v$VERSION.zip"

rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR/1.6/Assemblies"

cp -r About "$PACKAGE_DIR/"
cp loadFolders.xml "$PACKAGE_DIR/"
find 1.6/Assemblies -maxdepth 1 -type f \( -name "*.dll" -o -name "*.xml" \) -exec cp {} "$PACKAGE_DIR/1.6/Assemblies/" \;

rm -f "$ZIP_NAME"
(cd "$OUT_ROOT" && zip -r -q "../$ZIP_NAME" DisableSteamMods)

echo "Ok, $PWD/$ZIP_NAME ready for uploading to Workshop"
