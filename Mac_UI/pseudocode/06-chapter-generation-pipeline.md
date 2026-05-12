# 06 章节生成管道

图片：`../images/06-chapter-generation-pipeline.png`

对应功能：M4.4 `ChapterPipelinePage`。串联选章、FactSnapshot、生成门禁、CHANGES preview、保存应用。M6 后补 Humanize、Canonicalizer、WAL。

```pseudo
PageKey = "generate.pipeline"
View = ChapterPipelinePage
ViewModel = ChapterPipelineViewModel

state:
  selectedChapter: ChapterPlan
  factSnapshot: FactSnapshot
  trackingDebtSummary: List<TrackingDebt>       // M6.1
  rawGeneratedContent: String
  canonicalChanges: ChapterChanges              // M6.2
  humanizeResult: HumanizeRuleResult            // M6.2
  gateResult: GenerationGateResult
  walState: None | Pending | Recoverable | Committed
  executionLog: List<PipelineLogItem>
  canApply: Boolean

services:
  PortableFactSnapshotExtractor
  ContentGenerationPreparer
  ChapterGenerationPipeline
  GenerationGate
  ChangesProtocolParser
  ChangesCanonicalizer       // M6.2
  HumanizeRulePipeline       // M6.2
  ChapterWalRecoveryService  // M6.3

onLoad:
  chapters = LoadChapterPlans()
  if milestone >= M6.3:
    recoverableWal = ChapterWalRecoveryService.Scan()
    ShowRecoveryBanner(recoverableWal)

command SelectChapter(chapterId):
  selectedChapter = LoadChapterPlan(chapterId)
  factSnapshot = PortableFactSnapshotExtractor.ExtractSnapshot(selectedChapter)
  if milestone >= M6.1:
    trackingDebtSummary = TrackingDebtDetector.Detect(factSnapshot)
  gateResult = null
  rawGeneratedContent = ""

command GenerateChapter:
  promptContext = ContentGenerationPreparer.BuildPrompt(selectedChapter, factSnapshot)
  rawGeneratedContent = AI.Generate(promptContext, taskType = Writing)

  parsedChanges = ChangesProtocolParser.Parse(rawGeneratedContent)
  if milestone >= M6.2:
    canonicalChanges = ChangesCanonicalizer.Canonicalize(parsedChanges)
    humanizeResult = HumanizeRulePipeline.Run(rawGeneratedContent.Body)
    preparedContent = Merge(humanizeResult.Text, canonicalChanges)
  else:
    preparedContent = rawGeneratedContent

  gateResult = GenerationGate.Validate(preparedContent, factSnapshot)
  canApply = gateResult.Success

command ApplyGeneratedChapter:
  if not canApply: return
  result = ChapterGenerationPipeline.SaveGeneratedChapterStrictAsync(
    selectedChapter.Id,
    preparedContent,
    factSnapshot
  )
  if result.Success:
    Navigate("editor.workspace", selectedChapter.Id)
  else:
    ShowManualIntervention(result.InterventionHint)

command RecoverFromWal(walEntry):
  disabledUntil("M6.3 WAL")
  result = ChapterWalRecoveryService.Apply(walEntry)
  ShowRecoveryResult(result)

render:
  LeftPanel:
    chapterSelector(chapters)
    factSnapshotPreview(factSnapshot)
    trackingDebtSummary(trackingDebtSummary, disabledUntil M6.1)

  CenterPanel:
    stepList(["FactSnapshot", "生成", "Humanize", "CHANGES", "Gate", "WAL", "保存"])
    ChangesPreview(raw = parsedChanges, canonical = canonicalChanges)
    HumanizeWarnings(humanizeResult)
    GenerationGateResult(gateResult)

  RightPanel:
    button("开始生成", GenerateChapter)
    button("应用到章节", ApplyGeneratedChapter, enabled = canApply)
    WalStatus(walState, disabledUntil M6.3)
    executionLog(executionLog)
```
