# 02 主工作区三栏

图片：`../images/02-main-workspace-three-column.png`

对应功能：M3 `MainWindow` + `ThreeColumnLayoutView`。左侧导航、中间页面、右侧 AI 对话占位；M4.5 后右栏变成真实 `ConversationPanelView`。

```pseudo
PageKey = "dashboard"
View = MainWindow + ThreeColumnLayoutView
ViewModel = MainWindowViewModel + ThreeColumnLayoutViewModel

state:
  currentProject: ProjectContext
  navigationTree: List<NavigationNode>
  currentPageKey: PageKey
  centerContent: ViewModel
  rightPanel: RightConversationViewModel
  leftWidth: Number
  rightWidth: Number
  dashboardStats:
    totalWords
    chapterCount
    characterCount
    debtCount
    currentChapterProgress
  activityFeed: List<ActivityItem>

services:
  INavigationService
  PageRegistry
  WritingNavigationCatalog
  WindowStateStore
  FileProjectManager
  DispatcherScheduler

onStartup(projectId):
  currentProject = FileProjectManager.OpenProject(projectId)
  navigationTree = WritingNavigationCatalog.GetAllModules()
  layout = WindowStateStore.Load()
  leftWidth = layout.LeftWidth
  rightWidth = layout.RightWidth
  PageRegistry.Register("dashboard", DashboardView, DashboardViewModel)
  INavigationService.Navigate("dashboard")

onNavigate(pageKey):
  currentPageKey = pageKey
  centerContent = PageRegistry.ResolveViewModel(pageKey)

onColumnResize(leftWidth, rightWidth):
  WindowStateStore.Save({ leftWidth, rightWidth })

render:
  MainWindow:
    title(currentProject.Name)
    toolbar:
      button("搜索")
      button("同步状态", disabledForLocalOnly)
      button("设置")

  ThreeColumnLayout:
    LeftColumn(width = leftWidth):
      projectSummary(currentProject)
      navigationSection("写作", ["仪表盘", "草稿", "大纲", "角色", "世界观"])
      navigationSection("工具", ["AI 对话", "校验", "打包", "设置"])

    CenterColumn:
      if currentPageKey == "dashboard":
        statsCards(dashboardStats)
        chapterProgress(currentProject.ActiveChapter)
        activityFeed(activityFeed)
      else:
        contentControl(centerContent)

    RightColumn(width = rightWidth):
      if milestone < M4.5:
        placeholder("AI 助手将在 M4.5 接入")
      else:
        ConversationPanelView(rightPanel)
```
