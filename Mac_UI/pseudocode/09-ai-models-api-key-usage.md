# 09 AI 模型 / API Key / 用量

图片：`../images/09-ai-models-api-key-usage.png`

对应功能：M4.6 AI 管理。管理模型、供应商、API Key、提示词和用量；M5 后 API Key 走 Keychain；M6.6 后支持任务类型路由。

```pseudo
PageKeys:
  "ai.models"
  "ai.keys"
  "ai.prompts"
  "ai.usage"

View = AIManagementPage
ViewModel = AIManagementViewModel

state:
  providers: List<AIProvider>
  models: List<AIModelConfig>
  apiKeys: List<ApiKeyEntry>
  activeConfiguration: AIConfiguration
  taskRoutes:
    Chat: ModelRoute
    Writing: ModelRoute
    Polish: ModelRoute
    Validation: ModelRoute
    Embedding: ModelRoute
  usageSummary:
    requestCount
    tokenCount
    costEstimate
    successRate
  keyPoolStatus: KeyPoolStatus

services:
  FileAIConfigurationStore
  IApiKeySecretStore              // M5 接 MacOSKeychainApiKeySecretStore
  ApiKeyRotationService
  FileUsageStatisticsService
  FilePromptTemplateStore
  TaskModelRouter                 // M6.6
  ModelCapabilityStore            // M6.6

onLoad:
  providers = FileAIConfigurationStore.LoadProviders()
  models = FileAIConfigurationStore.LoadModels()
  activeConfiguration = FileAIConfigurationStore.GetActiveConfiguration()
  apiKeys = IApiKeySecretStore.ListKeyMetadata()
  usageSummary = FileUsageStatisticsService.GetTodaySummary()
  if milestone >= M6.6:
    taskRoutes = TaskModelRouter.LoadRoutes()

command SaveModelConfig(config):
  ValidateEndpoint(config.Endpoint)
  FileAIConfigurationStore.SaveConfiguration(config)

command SaveApiKey(providerId, apiKey):
  if milestone >= M5:
    IApiKeySecretStore.Set(providerId, apiKey)
  else:
    FileAIConfigurationStore.SaveTemporaryKey(providerId, apiKey)
  ApiKeyRotationService.UpdateKeyPool(providerId, LoadKeys(providerId))

command TestModel(modelId):
  result = AI.Generate("ping", taskType = Chat, modelId = modelId)
  ShowTestResult(result)

command SaveTaskRoute(taskType, route):
  disabledUntil("M6.6 TaskModelRouter")
  TaskModelRouter.SaveRoute(taskType, route)

render:
  LeftNav:
    navItem("模型与密钥", active)
    navItem("使用统计")
    navItem("提示词")
    navItem("日志")

  ModelTable:
    columns(["提供商", "模型", "端点", "状态", "操作"])
    rows(models)
    button("添加模型")

  ApiKeyPanel:
    keyRows(apiKeys)
    keyStatus(keyPoolStatus)
    button("新增 Key")
    toggle("启用")

  TaskRoutePanel:
    routeSelector("Chat", taskRoutes.Chat)
    routeSelector("Writing", taskRoutes.Writing)
    routeSelector("Polish", taskRoutes.Polish, disabledUntil M6.6)
    routeSelector("Validation", taskRoutes.Validation, disabledUntil M6.6)

  UsagePanel:
    stat("请求数", usageSummary.requestCount)
    stat("Tokens", usageSummary.tokenCount)
    stat("费用", usageSummary.costEstimate)
    stat("成功率", usageSummary.successRate)
```
