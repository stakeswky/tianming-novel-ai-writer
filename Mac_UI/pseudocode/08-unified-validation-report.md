# 08 统一校验报告

图片：`../images/08-unified-validation-report.png`

对应功能：M6.4 分层抽样 + 向量定位校验页面。M4.4 只需要 `ValidationIssueList` 用于生成门禁；完整报告在 M6.4 实现。

```pseudo
PageKey = "validation.report"
View = UnifiedValidationReportPage + ValidationIssueList
ViewModel = UnifiedValidationReportViewModel

state:
  selectedScope: Project | Volume | Chapter
  selectedVolume: Volume?
  summary:
    totalIssues
    errorCount
    warningCount
    infoCount
    sampledChapterCount
  issueGroups: List<ValidationIssueGroup>
  selectedIssue: ValidationIssue
  evidenceHits: List<ValidationVectorHit>
  repairHints: List<RepairHint>
  lastRunAt: DateTime?
  running: Boolean

services:
  PortableUnifiedValidationService
  RiskBasedChapterSampler       // M6.4
  TrackingDebtRuleChecker       // M6.4
  ValidationVectorLocator       // M6.4
  FileValidationReportStore
  PortableChapterRepairService

command RunValidation:
  running = true
  sample = RiskBasedChapterSampler.Sample(selectedScope)
  result = PortableUnifiedValidationService.Validate(sample)
  report = LayeredValidationReportBuilder.Build(result)
  FileValidationReportStore.Save(report)
  LoadReport(report.Id)
  running = false

command SelectIssue(issueId):
  selectedIssue = FindIssue(issueId)
  evidenceHits = selectedIssue.EvidenceHits
  repairHints = selectedIssue.RepairHints

command OpenEvidence(hit):
  Navigate("editor.workspace", hit.ChapterId)
  HighlightRange(hit.Range)

command RepairSelectedIssue:
  hint = PortableChapterRepairService.BuildHint(selectedIssue)
  Navigate("generate.pipeline", { repairHint: hint })

render:
  Header:
    tabs(["概览", "问题列表", "按章节", "修复建议"])
    button("运行校验", RunValidation)

  SummaryCards:
    card("总问题", summary.totalIssues)
    card("错误", summary.errorCount)
    card("警告", summary.warningCount)
    card("建议", summary.infoCount)
    donutChart(issueSeverityDistribution)

  IssueTable:
    columns(["章节", "错误", "警告", "建议", "状态"])
    rows(issueGroups)

  EvidencePanel:
    selectedIssueDetails(selectedIssue)
    evidenceHitList(evidenceHits)
    button("打开证据章节", OpenEvidence)
    button("生成修复建议", RepairSelectedIssue)

  Footer:
    lastRunAt
    sampledChapterCount
    exportReportButton()
```
