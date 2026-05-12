# 10 macOS 偏好 / 平台

图片：`../images/10-macos-preferences-platform.png`

对应功能：M5 macOS 平台能力。Keychain、系统代理、主题跟随、应用菜单、存储路径；不承接 v2.8.7 写作内核。

```pseudo
PageKey = "settings.platform"
View = MacOSPreferencesPage
ViewModel = MacOSPreferencesViewModel

state:
  keychainStatus: KeychainStatus
  proxySettings:
    useSystemProxy: Boolean
    httpProxy: Uri?
    httpsProxy: Uri?
    lastCheckedAt: DateTime?
  appearanceSettings:
    followSystem: Boolean
    currentAppearance: Light | Dark
  storagePaths:
    projectRoot: Path
    cacheRoot: Path
    backupRoot: Path
    logLevel: Info | Debug | Warning | Error
  menuShortcuts:
    newProject: "Cmd+N"
    openProject: "Cmd+O"
    save: "Cmd+S"
    preferences: "Cmd+,"
    quit: "Cmd+Q"

services:
  MacOSKeychainApiKeySecretStore
  MacOSSystemProxyService
  MacOSSystemAppearanceMonitor
  PortableThemeStateController
  AppPaths
  WindowStateStore

onLoad:
  keychainStatus = MacOSKeychainApiKeySecretStore.CheckStatus()
  proxySettings = MacOSSystemProxyService.ReadCurrentPolicy()
  appearanceSettings.currentAppearance = MacOSSystemAppearanceMonitor.ReadCurrent()
  storagePaths = AppPaths.LoadConfiguredPaths()

command ToggleSystemProxy(enabled):
  proxySettings.useSystemProxy = enabled
  if enabled:
    proxySettings = MacOSSystemProxyService.ReadCurrentPolicy()
  SavePreferences()

command RefreshProxy:
  proxySettings = MacOSSystemProxyService.ReadCurrentPolicy()
  proxySettings.lastCheckedAt = Now()

command ToggleFollowSystemTheme(enabled):
  appearanceSettings.followSystem = enabled
  if enabled:
    MacOSSystemAppearanceMonitor.Start()
    PortableThemeStateController.ApplySystemAppearance()
  else:
    MacOSSystemAppearanceMonitor.Stop()
  SavePreferences()

onSystemAppearanceChanged(appearance):
  if appearanceSettings.followSystem:
    PortableThemeStateController.OnSystemAppearanceChanged(appearance)

command ChangeStoragePath(kind):
  path = ShowFolderPicker(storagePaths[kind])
  if path is null: return
  storagePaths[kind] = path
  AppPaths.Save(storagePaths)

render:
  LeftNav:
    navItem("通用")
    navItem("编辑器")
    navItem("AI")
    navItem("校验")
    navItem("快捷键")
    navItem("外观")
    navItem("平台", active)
    navItem("备份")
    navItem("关于")

  PlatformSection:
    KeychainCard:
      status(keychainStatus)
      button("管理")

    ProxyCard:
      toggle("使用系统代理", proxySettings.useSystemProxy)
      readonlyField("HTTP 代理", proxySettings.httpProxy)
      readonlyField("HTTPS 代理", proxySettings.httpsProxy)
      button("刷新", RefreshProxy)

    AppearanceCard:
      toggle("跟随系统主题", appearanceSettings.followSystem)
      select("应用主题", ["自动", "浅色", "深色"])

    StorageCard:
      pathPicker("项目存储路径", storagePaths.projectRoot)
      pathPicker("缓存路径", storagePaths.cacheRoot)
      pathPicker("备份路径", storagePaths.backupRoot)
      select("日志级别", storagePaths.logLevel)

  Footer:
    button("恢复默认")
    button("应用", SavePreferences)
```
