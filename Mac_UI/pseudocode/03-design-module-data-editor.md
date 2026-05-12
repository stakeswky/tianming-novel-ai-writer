# 03 设计模块数据编辑

图片：`../images/03-design-module-data-editor.png`

对应功能：M4.1 `CategoryDataPageView`。统一承载世界观、角色、势力、地点、剧情、创意素材库 6 类设计数据。

```pseudo
PageKeys:
  "design.world"
  "design.character"
  "design.faction"
  "design.location"
  "design.plot"
  "design.materials"

View = CategoryDataPageView + DataFormView
ViewModel = CategoryDataPageViewModel

state:
  categorySpec: CategorySpec
  categoryTree: List<CategoryNode>
  selectedCategory: CategoryNode
  records: List<DesignRecord>
  selectedRecord: DesignRecord
  formSchema: DataSchema
  validationErrors: List<FieldError>
  dirty: Boolean

services:
  FileModuleDataStore
  WritingNavigationCatalog
  ProjectContextService
  FactSnapshotRebuildPlanner  // M6.1 后用于显示受影响章节

onNavigate(pageKey):
  categorySpec = CategorySpecRegistry.Resolve(pageKey)
  formSchema = categorySpec.Schema
  categoryTree = FileModuleDataStore.LoadCategories(categorySpec.StoragePath)
  records = FileModuleDataStore.LoadRecords(categorySpec.StoragePath, selectedCategory)

command NewRecord:
  selectedRecord = formSchema.CreateEmptyRecord()
  dirty = true

command SaveRecord:
  validationErrors = formSchema.Validate(selectedRecord)
  if validationErrors.any: return
  FileModuleDataStore.SaveRecord(categorySpec.StoragePath, selectedRecord)
  dirty = false
  if milestone >= M6.1:
    rebuildPlan = FactSnapshotRebuildPlanner.PlanFromDesignChange(selectedRecord)
    ShowAffectedChapters(rebuildPlan)

command DeleteRecord:
  Confirm("删除后可能影响章节事实快照")
  FileModuleDataStore.DeleteRecord(categorySpec.StoragePath, selectedRecord.Id)

render:
  LeftTree:
    moduleTabs(["世界观", "角色", "势力", "地点", "剧情", "素材"])
    categoryTree(categoryTree)

  MainForm:
    header(categorySpec.DisplayName)
    toolbar:
      button("新建", NewRecord)
      button("导入")
      button("导出")
    DataFormView(schema = formSchema, value = selectedRecord)
    tabPanel:
      tab("详情设定")
      tab("属性")
      tab("关联")
      tab("版本记录")
    if milestone >= M6.1:
      affectedChapterPreview(rebuildPlan)

  Footer:
    saveButton(enabled = dirty)
    validationSummary(validationErrors)
```
