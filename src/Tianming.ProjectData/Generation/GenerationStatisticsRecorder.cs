using System;
using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public class GenerationStatisticsRecorder
    {
        private const int MaxRecentRecords = 100;
        private GenerationStatistics _statistics;
        private readonly List<GenerationRecord> _recentRecords = new();
        private readonly object _statisticsLock = new();
        private readonly object _recordsLock = new();

        public GenerationStatisticsRecorder()
            : this(null)
        {
        }

        public GenerationStatisticsRecorder(GenerationStatistics? initialStatistics)
        {
            _statistics = initialStatistics ?? new GenerationStatistics();
        }

        public virtual void RecordGeneration(GenerationResult result)
        {
            var rewriteCount = result.TotalAttempts - 1;

            var record = new GenerationRecord
            {
                ChapterId = result.ChapterId,
                Success = result.Success,
                TotalAttempts = result.TotalAttempts,
                RewriteCount = rewriteCount,
                RequiresManualIntervention = result.RequiresManualIntervention,
                FailureReasons = result.GetLastFailureReasons()
            };

            foreach (var attempt in result.Attempts)
            {
                var attemptRecord = new AttemptRecord
                {
                    AttemptNumber = attempt.AttemptNumber,
                    Success = attempt.Success,
                    FailureReasons = attempt.FailureReasons
                };

                if (!attempt.Success && attempt.FailureReasons.Count > 0)
                {
                    var firstReason = attempt.FailureReasons.FirstOrDefault() ?? "";
                    if (firstReason.Contains("[Protocol]", StringComparison.Ordinal))
                        attemptRecord.FailureType = "Protocol";
                    else if (firstReason.Contains("[Consistency]", StringComparison.Ordinal))
                        attemptRecord.FailureType = "Consistency";

                    if (!string.IsNullOrEmpty(attemptRecord.FailureType))
                        record.FailureStages.Add($"Attempt{attempt.AttemptNumber}:{attemptRecord.FailureType}");
                }

                record.Attempts.Add(attemptRecord);
            }

            lock (_statisticsLock)
            {
                _statistics.TotalGenerations++;

                if (result.Success)
                {
                    if (result.TotalAttempts == 1)
                        _statistics.FirstPassCount++;
                    else
                        _statistics.RewritePassCount++;
                }
                else if (result.RequiresManualIntervention)
                {
                    _statistics.FinalFailureCount++;
                }

                if (!_statistics.RewriteDistribution.ContainsKey(rewriteCount))
                    _statistics.RewriteDistribution[rewriteCount] = 0;
                _statistics.RewriteDistribution[rewriteCount]++;

                foreach (var attempt in result.Attempts)
                {
                    if (attempt.Success || attempt.FailureReasons.Count == 0)
                        continue;

                    var firstReason = attempt.FailureReasons.FirstOrDefault() ?? "";
                    if (firstReason.Contains("[Protocol]", StringComparison.Ordinal))
                    {
                        _statistics.ProtocolFailureCount++;
                    }
                    else if (firstReason.Contains("[Consistency]", StringComparison.Ordinal))
                    {
                        _statistics.ConsistencyFailureCount++;
                        ParseConsistencyIssues(attempt.FailureReasons);
                    }
                }

                _statistics.EndTime = DateTime.Now;
            }

            lock (_recordsLock)
            {
                _recentRecords.Add(record);
                if (_recentRecords.Count > MaxRecentRecords)
                    _recentRecords.RemoveRange(0, _recentRecords.Count - MaxRecentRecords);
            }
        }

        public virtual void RecordConsistencyIssue(string issueType)
        {
            lock (_statisticsLock)
            {
                switch (issueType)
                {
                    case "CharacterStateConflict":
                        _statistics.ConsistencyIssues.CharacterStateConflict++;
                        break;
                    case "ForeshadowingEarlyPayoff":
                    case "PayoffBeforeSetup":
                        _statistics.ConsistencyIssues.ForeshadowingEarlyPayoff++;
                        break;
                    case "ForeshadowingRollback":
                        _statistics.ConsistencyIssues.ForeshadowingRollback++;
                        break;
                    case "ConflictStatusSkip":
                        _statistics.ConsistencyIssues.ConflictStatusSkip++;
                        break;
                    case "CharacterNotInvolved":
                        _statistics.ConsistencyIssues.CharacterNotInvolved++;
                        break;
                }
            }
        }

        public virtual GenerationStatistics GetStatistics()
        {
            lock (_statisticsLock)
            {
                return _statistics;
            }
        }

        public virtual List<GenerationRecord> GetRecentRecords(int count = 10)
        {
            lock (_recordsLock)
            {
                return _recentRecords.TakeLast(count).ToList();
            }
        }

        public virtual void ResetStatistics()
        {
            lock (_statisticsLock)
            {
                _statistics = new GenerationStatistics();
            }

            lock (_recordsLock)
            {
                _recentRecords.Clear();
            }
        }

        public string GetStatisticsSummary()
        {
            lock (_statisticsLock)
            {
                return $"生成统计: 总数={_statistics.TotalGenerations}, " +
                       $"首次通过={_statistics.FirstPassCount}({_statistics.FirstPassRate:F1}%), " +
                       $"重写通过={_statistics.RewritePassCount}, " +
                       $"最终失败={_statistics.FinalFailureCount}, " +
                       $"协议失败={_statistics.ProtocolFailureCount}, " +
                       $"一致性失败={_statistics.ConsistencyFailureCount}";
            }
        }

        private void ParseConsistencyIssues(List<string> reasons)
        {
            foreach (var reason in reasons)
            {
                if (reason.Contains("CharacterStateConflict", StringComparison.Ordinal))
                    RecordConsistencyIssue("CharacterStateConflict");
                if (reason.Contains("PayoffBeforeSetup", StringComparison.Ordinal))
                    RecordConsistencyIssue("PayoffBeforeSetup");
                if (reason.Contains("ForeshadowingRollback", StringComparison.Ordinal))
                    RecordConsistencyIssue("ForeshadowingRollback");
                if (reason.Contains("ConflictStatusSkip", StringComparison.Ordinal))
                    RecordConsistencyIssue("ConflictStatusSkip");
                if (reason.Contains("CharacterNotInvolved", StringComparison.Ordinal))
                    RecordConsistencyIssue("CharacterNotInvolved");
            }
        }
    }
}
