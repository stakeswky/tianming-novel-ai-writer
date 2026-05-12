# 01 欢迎 / 项目选择

图片：`../images/01-welcome-project-selector.png`

对应功能：M3 `WelcomeView`。负责新建项目、打开已有项目、显示最近项目、默认存储位置、本地模式状态。导入/恢复入口只作为 M6.9 备份恢复预留。

```pseudo
PageKey = "welcome"
View = WelcomeView
ViewModel = WelcomeViewModel

state:
  recentProjects: List<ProjectSummary>
  defaultProjectRoot: Path
  lastOpenedProject: ProjectSummary?
  isLocalMode: true
  keychainStatus: Connected | NotConnected | Unknown
  onnxStatus: Available | Optional | Missing
  selectedLayoutMode: Grid | List

services:
  FileProjectManager
  AppPaths
  WindowStateStore
  IApiKeySecretStore        // M5 接真实 Keychain，M4 前可显示未连接
  BackupRestorePlanner     // M6.9 前不可用

onLoad:
  defaultProjectRoot = AppPaths.DefaultProjectRoot
  recentProjects = FileProjectManager.LoadRecentProjects()
  lastOpenedProject = FileProjectManager.LoadLastOpened()
  keychainStatus = IApiKeySecretStore.GetStatusOrUnknown()

command NewProject:
  path = ShowFolderPicker(defaultProjectRoot)
  if path is null: return
  project = FileProjectManager.CreateProject(path, template = EmptyNovel)
  FileProjectManager.SetLastOpened(project)
  Navigate("dashboard", project.Id)

command OpenProject:
  path = ShowFolderPicker(defaultProjectRoot)
  if path is null: return
  project = FileProjectManager.OpenProject(path)
  FileProjectManager.AddRecent(project)
  Navigate("dashboard", project.Id)

command ContinueLastOpened:
  if lastOpenedProject is null: return disabled
  project = FileProjectManager.OpenProject(lastOpenedProject.Path)
  Navigate("dashboard", project.Id)

command ImportProject:
  disabledUntil("M6.9 Backup / Restore")
  plan = BackupRestorePlanner.BuildDryRunPlan(importFile)
  ShowRestorePreview(plan)

render:
  LeftRail:
    logo("天命")
    navItem("欢迎 / 项目选择", active)
    navItem("新建项目")
    navItem("打开项目")
    navItem("导入项目", disabledUntil M6.9)
    recentProjectList(recentProjects)
    button("清除最近记录")

  Main:
    hero("欢迎回来")
    actionCard("新建小说项目", NewProject)
    actionCard("打开已有项目", OpenProject)
    projectGrid(recentProjects, layout = selectedLayoutMode)
    storageLocation(defaultProjectRoot, ChangeDefaultRoot)
    lastOpenedCard(lastOpenedProject, ContinueLastOpened)
    localModeCard(isLocalMode)

  StatusBar:
    appVersion()
    runtime(".NET 8")
    badge("本地写作模式")
    badge("Keychain", keychainStatus)
    badge("ONNX", onnxStatus)
```
