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

## Fresh profile 兜底

新机器或重置过 `~/Library/Caches/com.apple.LaunchServices` 的 user profile 上首次运行，可能出现 `list_apps` 能列出 `Avalonia Application — dev.tianming.avalonia.manualtest` 但 `get_app_state(dev.tianming.avalonia.manualtest)` 仍返回 `appNotFound`——这是 LaunchServices DB 还没 pick up 新写入 `~/Applications/TianmingDev.app` 的签名。三步兜底：

1. 重建 LaunchServices DB 的 user-domain 条目：
   ```bash
   /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister \
       -kill -r -domain user
   ```
2. 让 Finder 触发 LS 重新注册新签名：
   ```bash
   open ~/Applications/TianmingDev.app
   ```
3. 重跑 Computer Use 验证：
   - `list_apps` 仍应看到 `Avalonia Application — dev.tianming.avalonia.manualtest`
   - `get_app_state("dev.tianming.avalonia.manualtest")` 应返回 accessibility 树（不是 `appNotFound`）

注意 `lsregister -kill -r -domain user` 只清理当前用户的 LS DB，不影响系统级条目，安全可复跑。如仍失败，检查 `~/Applications/TianmingDev.app` 是否真实存在 + `codesign -dvv` 输出 `Identifier=dev.tianming.avalonia.manualtest`。
