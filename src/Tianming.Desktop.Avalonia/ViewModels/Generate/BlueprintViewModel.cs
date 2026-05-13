using TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint;
using TM.Services.Modules.ProjectData.Modules.Generate.Blueprint;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class BlueprintViewModel
    : DataManagementViewModel<BlueprintCategory, BlueprintData, BlueprintSchema>
{
    public BlueprintViewModel(ModuleDataAdapter<BlueprintCategory, BlueprintData> adapter)
        : base(adapter) { }
}
