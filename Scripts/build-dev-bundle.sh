#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj"
CONFIGURATION="${CONFIGURATION:-Debug}"
FRAMEWORK="${FRAMEWORK:-net8.0}"
APP_DIR="${APP_DIR:-$HOME/Applications/TianmingDev.app}"
COMPAT_APP_LINK="${COMPAT_APP_LINK:-/tmp/TianmingDev.app}"
BUNDLE_ID="${BUNDLE_ID:-dev.tianming.avalonia.manualtest}"
BUNDLE_EXECUTABLE="${BUNDLE_EXECUTABLE:-TianmingDev}"
PUBLISH_DIR="$ROOT/src/Tianming.Desktop.Avalonia/bin/$CONFIGURATION/$FRAMEWORK"

dotnet build "$PROJECT" -c "$CONFIGURATION" --no-restore
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp -R "$PUBLISH_DIR/"* "$APP_DIR/Contents/MacOS/"
mv "$APP_DIR/Contents/MacOS/Tianming.Desktop.Avalonia" "$APP_DIR/Contents/MacOS/$BUNDLE_EXECUTABLE"
chmod +x "$APP_DIR/Contents/MacOS/$BUNDLE_EXECUTABLE"

cat > "$APP_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>$BUNDLE_EXECUTABLE</string>
  <key>CFBundleDisplayName</key><string>天命 Dev</string>
  <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>TianmingDev</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleDevelopmentRegion</key><string>zh_CN</string>
  <key>CFBundleSignature</key><string>TMNG</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>CFBundleShortVersionString</key><string>0.0.0-dev</string>
  <key>CFBundleSupportedPlatforms</key>
  <array>
    <string>MacOSX</string>
  </array>
  <key>LSMinimumSystemVersion</key><string>13.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
  <key>NSHumanReadableCopyright</key><string>Copyright 2026 Tianming</string>
</dict>
</plist>
PLIST

printf 'APPLTMNG' > "$APP_DIR/Contents/PkgInfo"
codesign --force --deep -s - "$APP_DIR"

if [[ "$APP_DIR" != "$COMPAT_APP_LINK" ]]; then
  rm -rf "$COMPAT_APP_LINK"
  ln -s "$APP_DIR" "$COMPAT_APP_LINK"
fi

echo "$APP_DIR"
