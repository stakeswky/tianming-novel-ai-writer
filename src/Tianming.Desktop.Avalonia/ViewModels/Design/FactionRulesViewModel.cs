using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class FactionRulesViewModel
    : DataManagementViewModel<FactionRulesCategory, FactionRulesData, FactionRulesSchema>
{
    public FactionRulesViewModel(ModuleDataAdapter<FactionRulesCategory, FactionRulesData> adapter)
        : base(adapter) { }
}
