# macOS Avalonia Manual Test How-To

## Computer Use attach flow

1. Run `Scripts/build-dev-bundle.sh`.
2. Launch `/tmp/TianmingDev.app`.
3. Attach Computer Use to bundle id `dev.tianming.avalonia.manualtest`.
4. Verify the visible window title is `天命`.

This wrapper exists because `dotnet run --project src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj` starts a visible process without a stable `CFBundleIdentifier`, which prevents Computer Use from attaching reliably on macOS.

## Bundle metadata checklist

`Scripts/build-dev-bundle.sh` writes the real test bundle to `~/Applications/TianmingDev.app`, then refreshes `/tmp/TianmingDev.app` as a compatibility symlink. Computer Use can list bundles launched from `/tmp`, but `get_app_state` resolves reliably only when LaunchServices sees the backing app in an application location.

The generated `.app` has a stable executable name (`TianmingDev`), `PkgInfo`, `CFBundleIdentifier`, `CFBundleDisplayName`, `CFBundleInfoDictionaryVersion`, `CFBundleDevelopmentRegion`, `CFBundleSignature`, `CFBundleSupportedPlatforms`, `LSMinimumSystemVersion`, `NSHighResolutionCapable`, `NSPrincipalClass`, and `NSHumanReadableCopyright`, then ad-hoc signs the full bundle with `codesign --force --deep -s -`.

Before launching, verify:

```bash
Scripts/build-dev-bundle.sh
plutil -p /tmp/TianmingDev.app/Contents/Info.plist
codesign -dvv ~/Applications/TianmingDev.app 2>&1
```

Expected: `codesign` reports `Identifier=dev.tianming.avalonia.manualtest` and signed Info.plist metadata such as `Info.plist entries=15` instead of the old `Info.plist=not bound`.

For the Computer Use gate, launch `/tmp/TianmingDev.app` and call `get_app_state` with `dev.tianming.avalonia.manualtest`. Passing evidence is a returned window tree for `TianmingDev`, not `appNotFound`.
