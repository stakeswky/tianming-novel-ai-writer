# 05 章节 Markdown 编辑器

图片：`../images/05-chapter-markdown-editor.png`

对应功能：M4.3 `EditorWorkspaceView`。提供 Markdown 编辑、多标签、章节元数据、实时字数统计、预览和保存。

```pseudo
PageKey = "editor.workspace"
View = EditorWorkspaceView + MarkdownEditor + ChapterTabBar
ViewModel = EditorWorkspaceViewModel

state:
  openTabs: List<ChapterTab>
  activeTab: ChapterTab
  documentText: String
  previewHtmlOrBlocks: PreviewDocument
  chapterMetadata:
    title
    volume
    chapterNumber
    relatedCharacters
    relatedLocations
    tags
  wordCount: Number
  savedAt: DateTime?
  isDirty: Boolean
  autosaveEnabled: Boolean

services:
  ChapterContentStore
  FileModuleDataStore
  EditorTabStateStore
  MarkdownPreviewRenderer
  DispatcherScheduler
  ChapterWalRecoveryService  // M6.3 后用于提示可恢复草稿

onLoad:
  openTabs = EditorTabStateStore.Load(projectId)
  if openTabs.empty:
    openTabs = LoadRecentChapterTab()
  OpenTab(openTabs.active)
  if milestone >= M6.3:
    recoveryItems = ChapterWalRecoveryService.Scan()
    ShowRecoverableDraftBadge(recoveryItems)

command OpenChapter(chapterId):
  text = ChapterContentStore.LoadChapter(chapterId)
  metadata = ChapterContentStore.LoadMetadata(chapterId)
  AddOrActivateTab(chapterId, text, metadata)

onTextChanged(text):
  documentText = text
  wordCount = CountWords(text)
  previewHtmlOrBlocks = MarkdownPreviewRenderer.Render(text)
  isDirty = true
  if autosaveEnabled:
    Debounce(AutoSave, 1500ms)

command SaveChapter:
  ChapterContentStore.SaveChapter(activeTab.chapterId, documentText, chapterMetadata)
  EditorTabStateStore.Save(openTabs)
  isDirty = false
  savedAt = Now()

command CloseTab(tab):
  if tab.isDirty:
    AskSaveDiscardCancel()
  RemoveTab(tab)
  EditorTabStateStore.Save(openTabs)

render:
  TopBar:
    ChapterTabBar(openTabs, activeTab)
    status(savedAt, isDirty, wordCount)
    buttons(["保存", "预览", "更多"])

  EditorArea:
    splitView:
      MarkdownEditor(text = documentText)
      MarkdownPreview(previewHtmlOrBlocks)

  RightMetadataPanel:
    form(chapterMetadata)
    relatedEntityPicker()
    tagsEditor()

  BottomStatus:
    wordCount
    cursorPosition
    autosaveState
    if milestone >= M6.3:
      recoverableDraftState()
```
