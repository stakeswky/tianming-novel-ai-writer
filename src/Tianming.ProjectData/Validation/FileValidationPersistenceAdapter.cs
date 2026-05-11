using System;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using TM.Services.Modules.ProjectData.Models.Validation;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileValidationPersistenceAdapter
    {
        private readonly FileValidationReportStore _reportStore;
        private readonly FileValidationSummaryStore _summaryStore;

        public FileValidationPersistenceAdapter(
            FileValidationReportStore reportStore,
            FileValidationSummaryStore summaryStore)
        {
            _reportStore = reportStore ?? throw new ArgumentNullException(nameof(reportStore));
            _summaryStore = summaryStore ?? throw new ArgumentNullException(nameof(summaryStore));
        }

        public Task SaveChapterReportAsync(ValidationReport report)
        {
            return _reportStore.SaveReportAsync(report);
        }

        public void SaveVolumeSummary(int volumeNumber, ValidationSummaryData summary)
        {
            _summaryStore.SaveVolumeValidation(volumeNumber, summary);
        }
    }
}
