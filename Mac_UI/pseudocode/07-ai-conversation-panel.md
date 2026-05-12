# 07 AI 对话面板

图片：`../images/07-ai-conversation-panel.png`

对应功能：M4.5 右栏 `ConversationPanelView`。支持 Ask / Plan / Agent 三模式、流式输出、thinking 分流、引用下拉、工具调用卡片。M6.7 后工具写入必须走待确认变更。

```pseudo
Container = RightConversationView
View = ConversationPanelView + ChatStreamView + ToolCallCard + ReferenceDropdown
ViewModel = ConversationPanelViewModel

state:
  mode: Ask | Plan | Agent
  currentSession: ChatSession
  messages: ObservableCollection<ChatMessage>
  streamingDeltaBuffer: List<ChatStreamDelta>
  thinkingBlocks: List<ThinkingBlock>
  inputText: String
  referenceQuery: String?
  referenceCandidates: List<ReferenceItem>
  toolCalls: List<ToolCallState>
  stagedEdits: List<StagedEdit>       // M6.7

services:
  ConfiguredAITextGenerationService
  FileSessionStore
  ReferenceParser
  ReferenceCatalog
  ReferenceExpansionService
  TodoExecutionService
  StagedEditStore      // M6.7
  StagedEditApplier    // M6.7

onLoad(projectId):
  currentSession = FileSessionStore.OpenOrCreate(projectId)
  messages = currentSession.Messages

onInputChanged(text):
  inputText = text
  if text.endsWith("@query"):
    referenceQuery = ExtractReferenceQuery(text)
    referenceCandidates = ReferenceCatalog.Search(referenceQuery)

command InsertReference(reference):
  inputText = ReplaceReferenceQuery(inputText, reference.Token)

command SendMessage:
  userMessage = BuildMessage(inputText, mode)
  messages.Add(userMessage)
  FileSessionStore.Save(currentSession)
  inputText = ""

  context = ReferenceExpansionService.Expand(userMessage.References)
  stream = AI.Stream(userMessage.Text, context, taskType = Chat)

  for delta in stream:
    if delta.kind == Thinking:
      thinkingBlocks.Add(delta)
    else:
      BufferAndFlushEvery16ms(delta, messages)

  FileSessionStore.Save(currentSession)

command ExecutePlanStep(step):
  if mode != Plan: return
  TodoExecutionService.Execute(step)

command ApproveToolCall(toolCall):
  if milestone < M6.7:
    return disabled
  stagedEdit = StagedEditStore.Load(toolCall.StagedEditId)
  result = StagedEditApplier.Apply(stagedEdit)
  ShowApplyResult(result)

command RejectToolCall(toolCall):
  StagedEditStore.MarkRejected(toolCall.StagedEditId)

render:
  Header:
    segmentedControl(["Ask", "Plan", "Agent"], mode)
    buttons(["新会话", "历史", "关闭"])

  ChatStream:
    for message in messages:
      ChatMessageBubble(message)
      if message.thinking:
        ThinkingDisclosure(message.thinking)
      for toolCall in message.toolCalls:
        ToolCallCard(toolCall)

  ToolCallCard:
    show tool name, arguments, status, latency
    if toolCall.hasStagedEdit:
      diffPreview(stagedEdit)
      buttons(["确认应用", "拒绝"])
    else:
      readonlyResult()

  Composer:
    ReferenceDropdown(referenceCandidates)
    textInput(inputText)
    sendButton(SendMessage)
```
