using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class PlotRulesViewModel
    : DataManagementViewModel<PlotRulesCategory, PlotRulesData, PlotRulesSchema>
{
    public PlotRulesViewModel(ModuleDataAdapter<PlotRulesCategory, PlotRulesData> adapter)
        : base(adapter) { }
}
