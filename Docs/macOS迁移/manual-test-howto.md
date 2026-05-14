# macOS Avalonia Manual Test How-To

## Computer Use attach flow

1. Run `Scripts/build-dev-bundle.sh`.
2. Launch `/tmp/TianmingDev.app`.
3. Attach Computer Use to bundle id `dev.tianming.avalonia.manualtest`.
4. Verify the visible window title is `天命`.

This wrapper exists because `dotnet run --project src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj` starts a visible process without a stable `CFBundleIdentifier`, which prevents Computer Use from attaching reliably on macOS.
