using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Modules.Generate.Outline;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class OutlineViewModel
    : DataManagementViewModel<OutlineCategory, OutlineData, OutlineSchema>
{
    public OutlineViewModel(ModuleDataAdapter<OutlineCategory, OutlineData> adapter)
        : base(adapter) { }
}
