using System;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using TM.Services.Modules.ProjectData.Models.Validation;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class PortableUnifiedValidationService : IUnifiedValidationService
    {
        private readonly Func<string, CancellationToken, Task<ChapterValidationResult>> _validateChapterAsync;
        private readonly Func<int, Task<VolumeValidationProcessOutput>> _validateVolumeAsync;
        private readonly Func<ValidationReport, Task>? _saveChapterReportAsync;
        private readonly Action<int, ValidationSummaryData>? _saveVolumeSummary;
        private readonly Func<Task<bool>> _needsRepublishAsync;

        public PortableUnifiedValidationService(
            Func<string, CancellationToken, Task<ChapterValidationResult>> validateChapterAsync,
            Func<int, Task<VolumeValidationProcessOutput>> validateVolumeAsync,
            Func<ValidationReport, Task>? saveChapterReportAsync = null,
            Action<int, ValidationSummaryData>? saveVolumeSummary = null,
            Func<Task<bool>>? needsRepublish = null)
        {
            _validateChapterAsync = validateChapterAsync;
            _validateVolumeAsync = validateVolumeAsync;
            _saveChapterReportAsync = saveChapterReportAsync;
            _saveVolumeSummary = saveVolumeSummary;
            _needsRepublishAsync = needsRepublish ?? (() => Task.FromResult(false));
        }

        public async Task<ChapterValidationResult> ValidateChapterAsync(
            string chapterId,
            CancellationToken ct = default)
        {
            var result = await _validateChapterAsync(chapterId, ct).ConfigureAwait(false);
            if (_saveChapterReportAsync != null)
            {
                var report = ValidationReportBuilder.BuildChapterReport(result);
                await _saveChapterReportAsync(report).ConfigureAwait(false);
            }

            return result;
        }

        public async Task<VolumeValidationResult> ValidateVolumeAsync(
            int volumeNumber,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var output = await _validateVolumeAsync(volumeNumber).ConfigureAwait(false);
            if (output.Summary != null)
                _saveVolumeSummary?.Invoke(volumeNumber, output.Summary);

            return output.Result;
        }

        public Task<bool> NeedsRepublishAsync()
        {
            return _needsRepublishAsync();
        }
    }
}
