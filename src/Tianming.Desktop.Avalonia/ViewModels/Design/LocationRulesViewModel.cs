using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class LocationRulesViewModel
    : DataManagementViewModel<LocationRulesCategory, LocationRulesData, LocationRulesSchema>
{
    public LocationRulesViewModel(ModuleDataAdapter<LocationRulesCategory, LocationRulesData> adapter)
        : base(adapter) { }
}
