using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterTrackingDispatcher
    {
        private readonly IChapterTrackingSink _sink;

        public ChapterTrackingDispatcher(IChapterTrackingSink sink)
        {
            _sink = sink;
        }

        public async Task DispatchAsync(string chapterId, ChapterChanges changes)
        {
            if (changes == null)
                return;

            foreach (var change in changes.CharacterStateChanges ?? new())
                await _sink.UpdateCharacterStateAsync(chapterId, change).ConfigureAwait(false);

            foreach (var change in changes.ConflictProgress ?? new())
                await _sink.UpdateConflictProgressAsync(chapterId, change).ConfigureAwait(false);

            foreach (var change in changes.NewPlotPoints ?? new())
                await _sink.AddPlotPointAsync(chapterId, change).ConfigureAwait(false);

            foreach (var action in changes.ForeshadowingActions ?? new())
            {
                if (string.Equals(action.Action, "setup", StringComparison.OrdinalIgnoreCase))
                    await _sink.MarkForeshadowingAsSetupAsync(action.ForeshadowId, chapterId).ConfigureAwait(false);
                else if (string.Equals(action.Action, "payoff", StringComparison.OrdinalIgnoreCase))
                    await _sink.MarkForeshadowingAsResolvedAsync(action.ForeshadowId, chapterId).ConfigureAwait(false);
            }

            foreach (var change in changes.LocationStateChanges ?? new())
                await _sink.UpdateLocationStateAsync(chapterId, change).ConfigureAwait(false);

            foreach (var change in changes.FactionStateChanges ?? new())
                await _sink.UpdateFactionStateAsync(chapterId, change).ConfigureAwait(false);

            if (changes.TimeProgression != null)
                await _sink.UpdateTimeProgressionAsync(chapterId, changes.TimeProgression).ConfigureAwait(false);

            if (changes.CharacterMovements != null && changes.CharacterMovements.Count > 0)
                await _sink.UpdateCharacterMovementsAsync(chapterId, changes.CharacterMovements).ConfigureAwait(false);

            foreach (var change in changes.ItemTransfers ?? new())
                await _sink.UpdateItemStateAsync(chapterId, change).ConfigureAwait(false);

            await _sink.RefreshForeshadowingOverdueStatusAsync(chapterId).ConfigureAwait(false);
        }

        public async Task RemoveChapterDataAsync(string chapterId)
        {
            await _sink.RemoveCharacterStateAsync(chapterId).ConfigureAwait(false);
            await _sink.RemoveConflictProgressAsync(chapterId).ConfigureAwait(false);
            await _sink.RemovePlotPointsAsync(chapterId).ConfigureAwait(false);
            await _sink.RemoveForeshadowingStatusAsync(chapterId).ConfigureAwait(false);
            await _sink.RemoveLocationStateAsync(chapterId).ConfigureAwait(false);
            await _sink.RemoveFactionStateAsync(chapterId).ConfigureAwait(false);
            await _sink.RemoveTimelineAsync(chapterId).ConfigureAwait(false);
            await _sink.RemoveItemStateAsync(chapterId).ConfigureAwait(false);
        }

        public Task RecordTrackingDebtsAsync(string chapterId, IReadOnlyList<TrackingDebt> debts)
        {
            return _sink.RecordTrackingDebtsAsync(chapterId, debts);
        }
    }

    public interface IChapterTrackingSink
    {
        Task UpdateCharacterStateAsync(string chapterId, CharacterStateChange change);
        Task UpdateConflictProgressAsync(string chapterId, ConflictProgressChange change);
        Task AddPlotPointAsync(string chapterId, PlotPointChange change);
        Task MarkForeshadowingAsSetupAsync(string foreshadowId, string chapterId);
        Task MarkForeshadowingAsResolvedAsync(string foreshadowId, string chapterId);
        Task RefreshForeshadowingOverdueStatusAsync(string chapterId);
        Task UpdateLocationStateAsync(string chapterId, LocationStateChange change);
        Task UpdateFactionStateAsync(string chapterId, FactionStateChange change);
        Task UpdateTimeProgressionAsync(string chapterId, TimeProgressionChange change);
        Task UpdateCharacterMovementsAsync(string chapterId, List<CharacterMovementChange> movements);
        Task UpdateItemStateAsync(string chapterId, ItemTransferChange change);
        Task RecordTrackingDebtsAsync(string chapterId, IReadOnlyList<TrackingDebt> debts);
        Task<IReadOnlyList<TrackingDebt>> LoadTrackingDebtsAsync(int volume);
        Task RemoveCharacterStateAsync(string chapterId);
        Task RemoveConflictProgressAsync(string chapterId);
        Task RemovePlotPointsAsync(string chapterId);
        Task RemoveForeshadowingStatusAsync(string chapterId);
        Task RemoveLocationStateAsync(string chapterId);
        Task RemoveFactionStateAsync(string chapterId);
        Task RemoveTimelineAsync(string chapterId);
        Task RemoveItemStateAsync(string chapterId);
    }
}
