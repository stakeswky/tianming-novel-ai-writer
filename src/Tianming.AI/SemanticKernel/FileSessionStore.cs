using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.Common.Helpers.Id;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;

namespace TM.Services.Framework.AI.SemanticKernel
{
    public sealed class FileSessionStore : IFileSessionStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly string _sessionsDir;
        private readonly Func<string?>? _currentChapterIdProvider;
        private readonly Dictionary<string, SessionInfo> _sessionIndex = new();
        private string? _currentSessionId;

        public FileSessionStore(string sessionsDir, Func<string?>? currentChapterIdProvider = null)
        {
            if (string.IsNullOrWhiteSpace(sessionsDir))
                throw new ArgumentException("会话目录不能为空", nameof(sessionsDir));

            _sessionsDir = sessionsDir;
            _currentChapterIdProvider = currentChapterIdProvider;
            Directory.CreateDirectory(_sessionsDir);
            LoadSessionIndex();
            CleanupEmptySessions();
        }

        public string CreateSession(string? title = null)
        {
            var sessionId = ShortIdGenerator.NewGuid().ToString("N")[..8];
            var now = DateTime.Now;
            var info = new SessionInfo
            {
                Id = sessionId,
                Title = title ?? $"会话 {now:MM-dd HH:mm}",
                CreatedAt = now,
                UpdatedAt = now,
                ContextChapterId = _currentChapterIdProvider?.Invoke()
            };

            _sessionIndex[sessionId] = info;
            _currentSessionId = sessionId;
            SaveSessionIndex();
            SaveMessages(sessionId, Array.Empty<SerializedMessageRecord>());
            return sessionId;
        }

        public string? GetCurrentSessionIdOrNull()
        {
            return _currentSessionId;
        }

        public IReadOnlyList<SessionInfo> GetAllSessions()
        {
            return _sessionIndex.Values
                .OrderByDescending(session => session.UpdatedAt)
                .Select(CloneSession)
                .ToList();
        }

        public void DeleteSession(string sessionId)
        {
            if (!_sessionIndex.Remove(sessionId))
                return;

            var messagesPath = GetMessagesFilePath(sessionId);
            if (File.Exists(messagesPath))
                File.Delete(messagesPath);

            SaveSessionIndex();

            if (_currentSessionId == sessionId)
                _currentSessionId = _sessionIndex.Keys.FirstOrDefault();
        }

        public void RenameSession(string sessionId, string newTitle)
        {
            if (!_sessionIndex.TryGetValue(sessionId, out var info))
                return;

            info.Title = newTitle;
            info.UpdatedAt = DateTime.Now;
            SaveSessionIndex();
        }

        public void UpdateSessionMode(string sessionId, string mode)
        {
            if (!_sessionIndex.TryGetValue(sessionId, out var info))
                return;

            info.Mode = mode;
            info.UpdatedAt = DateTime.Now;
            SaveSessionIndex();
        }

        public string GetSessionMode(string sessionId)
        {
            return _sessionIndex.TryGetValue(sessionId, out var info) ? info.Mode ?? "0" : "0";
        }

        public void SaveMessages(string sessionId, IEnumerable<SerializedMessageRecord> messages)
        {
            var list = messages?.ToList() ?? new List<SerializedMessageRecord>();
            if (_sessionIndex.TryGetValue(sessionId, out var info))
            {
                info.UpdatedAt = DateTime.Now;
                info.MessageCount = list.Count;
                var currentChapterId = _currentChapterIdProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(currentChapterId))
                    info.ContextChapterId = currentChapterId;
            }

            WriteJsonAtomic(GetMessagesFilePath(sessionId), list);
            SaveSessionIndex();
        }

        public IReadOnlyList<SerializedMessageRecord> LoadMessages(string sessionId)
        {
            var filePath = GetMessagesFilePath(sessionId);
            if (!File.Exists(filePath))
                return new List<SerializedMessageRecord>();

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<SerializedMessageRecord>>(json, JsonOptions)
                    ?? new List<SerializedMessageRecord>();
            }
            catch (JsonException)
            {
                return new List<SerializedMessageRecord>();
            }
            catch (IOException)
            {
                return new List<SerializedMessageRecord>();
            }
        }

        public Task SaveSessionAsync(ConversationSession session, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!_sessionIndex.TryGetValue(session.Id, out var info))
            {
                info = new SessionInfo
                {
                    Id = session.Id,
                    CreatedAt = session.CreatedAt,
                };
                _sessionIndex[session.Id] = info;
            }

            info.Title = string.IsNullOrWhiteSpace(session.Title) ? info.Title : session.Title;
            info.Mode = ((int)session.Mode).ToString();
            info.UpdatedAt = DateTime.Now;
            info.MessageCount = session.History.Count;

            SaveMessages(session.Id, session.History.Select(ToSerializedRecord));
            return Task.CompletedTask;
        }

        public Task<ConversationSession?> LoadSessionAsync(string sessionId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (!_sessionIndex.TryGetValue(sessionId, out var info))
                return Task.FromResult<ConversationSession?>(null);

            var session = new ConversationSession
            {
                Id = info.Id,
                Title = info.Title,
                CreatedAt = info.CreatedAt,
                Mode = ParseChatMode(info.Mode),
            };

            foreach (var record in LoadMessages(sessionId))
            {
                session.History.Add(ToConversationMessage(record));
            }

            return Task.FromResult<ConversationSession?>(session);
        }

        public Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var sessions = GetAllSessions()
                .Select(session => new SessionSummary
                {
                    Id = session.Id,
                    Title = session.Title,
                    UpdatedAt = session.UpdatedAt,
                    MessageCount = session.MessageCount,
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<SessionSummary>>(sessions);
        }

        public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            DeleteSession(sessionId);
            return Task.CompletedTask;
        }

        private string GetMessagesFilePath(string sessionId)
        {
            return Path.Combine(_sessionsDir, $"{sessionId}.messages.json");
        }

        private string GetIndexFilePath()
        {
            return Path.Combine(_sessionsDir, "_index.json");
        }

        private void LoadSessionIndex()
        {
            var indexPath = GetIndexFilePath();
            if (!File.Exists(indexPath))
                return;

            try
            {
                var json = File.ReadAllText(indexPath);
                var sessions = JsonSerializer.Deserialize<List<SessionInfo>>(json, JsonOptions);
                if (sessions == null)
                    return;

                foreach (var session in sessions.Where(session => !string.IsNullOrWhiteSpace(session.Id)))
                    _sessionIndex[session.Id] = session;
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        private void SaveSessionIndex()
        {
            WriteJsonAtomic(GetIndexFilePath(), _sessionIndex.Values.ToList());
        }

        private void CleanupEmptySessions()
        {
            var toDelete = new List<string>();
            foreach (var (id, info) in _sessionIndex)
            {
                if (info.MessageCount > 0)
                    continue;

                var path = GetMessagesFilePath(id);
                if (!File.Exists(path))
                {
                    toDelete.Add(id);
                    continue;
                }

                try
                {
                    var json = File.ReadAllText(path);
                    if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                        toDelete.Add(id);
                }
                catch (IOException)
                {
                    toDelete.Add(id);
                }
            }

            foreach (var id in toDelete)
            {
                _sessionIndex.Remove(id);
                var path = GetMessagesFilePath(id);
                if (File.Exists(path))
                    File.Delete(path);
            }

            if (toDelete.Count > 0)
                SaveSessionIndex();
        }

        private static void WriteJsonAtomic<T>(string path, T value)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(tempPath, path, overwrite: true);
        }

        private static SessionInfo CloneSession(SessionInfo session)
        {
            return new SessionInfo
            {
                Id = session.Id,
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                MessageCount = session.MessageCount,
                Mode = session.Mode,
                ContextChapterId = session.ContextChapterId
            };
        }

        private static SerializedMessageRecord ToSerializedRecord(ConversationMessage message)
        {
            return new SerializedMessageRecord
            {
                MessageId = message.RunId == Guid.Empty ? Guid.NewGuid().ToString("N") : message.RunId.ToString("N"),
                Role = message.Role.ToString().ToLowerInvariant(),
                Summary = message.Summary,
                Analysis = string.IsNullOrWhiteSpace(message.AnalysisRaw) ? null : message.AnalysisRaw,
                Timestamp = message.Timestamp,
            };
        }

        private static ConversationMessage ToConversationMessage(SerializedMessageRecord record)
        {
            return new ConversationMessage
            {
                RunId = Guid.TryParse(record.MessageId, out var runId) ? runId : Guid.Empty,
                Role = ParseConversationRole(record.Role),
                Timestamp = record.Timestamp,
                Summary = record.Summary,
                AnalysisRaw = record.Analysis ?? string.Empty,
            };
        }

        private static ConversationRole ParseConversationRole(string? role)
        {
            return role?.ToLowerInvariant() switch
            {
                "user" => ConversationRole.User,
                "system" => ConversationRole.System,
                _ => ConversationRole.Assistant,
            };
        }

        private static ChatMode ParseChatMode(string? mode)
        {
            return mode switch
            {
                "1" => ChatMode.Agent,
                "2" => ChatMode.Plan,
                _ => ChatMode.Ask,
            };
        }
    }

    public class SessionInfo
    {
        [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("Title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("CreatedAt")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("UpdatedAt")] public DateTime UpdatedAt { get; set; }
        [JsonPropertyName("MessageCount")] public int MessageCount { get; set; }
        [JsonPropertyName("Mode")] public string Mode { get; set; } = "0";
        [JsonPropertyName("ContextChapterId")] public string? ContextChapterId { get; set; }
    }

    public class SerializedMessageRecord
    {
        [JsonPropertyName("MessageId")] public string MessageId { get; set; } = string.Empty;
        [JsonPropertyName("Role")] public string Role { get; set; } = string.Empty;
        [JsonPropertyName("Summary")] public string Summary { get; set; } = string.Empty;
        [JsonPropertyName("Analysis")] public string? Analysis { get; set; }
        [JsonPropertyName("DurationSeconds")] public double? DurationSeconds { get; set; }
        [JsonPropertyName("ChangesJson")] public string? ChangesJson { get; set; }
        [JsonPropertyName("ChangesDurationSeconds")] public double? ChangesDurationSeconds { get; set; }
        [JsonPropertyName("PayloadType")] public int PayloadType { get; set; }
        [JsonPropertyName("PayloadJson")] public string? PayloadJson { get; set; }
        [JsonPropertyName("Timestamp")] public DateTime Timestamp { get; set; }
        [JsonPropertyName("ReferencesJson")] public string? ReferencesJson { get; set; }
    }
}
