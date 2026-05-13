using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class WorldRulesViewModel
    : DataManagementViewModel<WorldRulesCategory, WorldRulesData, WorldRulesSchema>
{
    public WorldRulesViewModel(ModuleDataAdapter<WorldRulesCategory, WorldRulesData> adapter)
        : base(adapter) { }
}
