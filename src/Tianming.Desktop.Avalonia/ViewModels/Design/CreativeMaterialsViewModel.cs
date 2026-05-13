using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class CreativeMaterialsViewModel
    : DataManagementViewModel<CreativeMaterialCategory, CreativeMaterialData, CreativeMaterialsSchema>
{
    public CreativeMaterialsViewModel(ModuleDataAdapter<CreativeMaterialCategory, CreativeMaterialData> adapter)
        : base(adapter) { }
}
