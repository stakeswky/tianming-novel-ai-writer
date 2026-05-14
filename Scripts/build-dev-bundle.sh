#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj"
CONFIGURATION="${CONFIGURATION:-Debug}"
FRAMEWORK="${FRAMEWORK:-net8.0}"
APP_DIR="${APP_DIR:-/tmp/TianmingDev.app}"
BUNDLE_ID="${BUNDLE_ID:-dev.tianming.avalonia.manualtest}"
PUBLISH_DIR="$ROOT/src/Tianming.Desktop.Avalonia/bin/$CONFIGURATION/$FRAMEWORK"

dotnet build "$PROJECT" -c "$CONFIGURATION" --no-restore
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp -R "$PUBLISH_DIR/"* "$APP_DIR/Contents/MacOS/"
chmod +x "$APP_DIR/Contents/MacOS/Tianming.Desktop.Avalonia"

cat > "$APP_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>Tianming.Desktop.Avalonia</string>
  <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
  <key>CFBundleName</key><string>TianmingDev</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>CFBundleShortVersionString</key><string>0.0.0-dev</string>
</dict>
</plist>
PLIST

echo "$APP_DIR"
