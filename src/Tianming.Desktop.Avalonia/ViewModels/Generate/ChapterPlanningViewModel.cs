using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class ChapterPlanningViewModel
    : DataManagementViewModel<ChapterCategory, ChapterData, ChapterPlanningSchema>
{
    public ChapterPlanningViewModel(ModuleDataAdapter<ChapterCategory, ChapterData> adapter)
        : base(adapter) { }
}
