# 04 生成规划

图片：`../images/04-generation-planning.png`

对应功能：M4.2 生成规划。承载大纲、分卷设计、章节规划、内容配置；一键成书按钮作为 M6.8 预留。

```pseudo
PageKeys:
  "generate.outline"
  "generate.volume"
  "generate.chapter"
  "generate.contentconfig"
  "generate.oneclick"      // M6.8 预留

View = GenerationPlanningPage
ViewModel = GenerationPlanningViewModel

state:
  activeTab: Outline | Volume | Chapter | ContentConfig
  outline: OutlineData
  volumes: List<VolumeDesign>
  chapters: List<ChapterPlan>
  contentConfig:
    targetWords
    tone
    perspective
    chapterCount
  dependencyStatus: List<GenerationDependency>
  dirty: Boolean

services:
  FileModuleDataStore
  ContentGenerationPreparer
  OneClickBookDependencyChecker  // M6.8
  OneClickBookPipeline           // M6.8

onLoad:
  outline = Load("Generate/GlobalSettings/Outline")
  volumes = Load("Generate/Elements/VolumeDesign")
  chapters = Load("Generate/Elements/Chapter")
  contentConfig = Load("Generate/Content/Content")
  dependencyStatus = CheckBasicDependencies()

command SavePlanning:
  Validate(outline, volumes, chapters, contentConfig)
  SaveAll()
  dirty = false

command GenerateMissingPlan:
  prompt = ContentGenerationPreparer.BuildPlanningPrompt(activeTab)
  result = AI.Generate(prompt, taskType = Writing)
  PreviewAndApply(result)

command RunOneClick:
  disabledUntil("M6.8 OneClickBookPipeline")
  plan = OneClickBookDependencyChecker.Check(project)
  if plan.hasMissingRequiredData:
    ShowDependencyPlan(plan)
  else:
    OneClickBookPipeline.StartOrResume(project)

render:
  Header:
    tabs(["大纲", "卷设计", "章节规划", "内容配置"])
    button("一键成书", disabledUntil M6.8)

  OutlineTab:
    textField("书名")
    textField("子标题")
    numberField("目标字数")
    richText("核心简介")

  VolumeTab:
    volumeTable(volumes)
    buttons(["新增卷", "展开章节", "保存"])

  ChapterTab:
    chapterPlanTable(chapters)
    dependencyStatus(dependencyStatus)

  ContentConfigTab:
    controls(targetWords, tone, perspective)
    SavePlanningButton()

  if milestone >= M6.8:
    OneClickProgressPanel(currentStep, checkpoints)
```
