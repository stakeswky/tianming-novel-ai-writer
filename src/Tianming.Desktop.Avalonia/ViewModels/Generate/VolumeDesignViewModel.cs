using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class VolumeDesignViewModel
    : DataManagementViewModel<VolumeDesignCategory, VolumeDesignData, VolumeDesignSchema>
{
    public VolumeDesignViewModel(ModuleDataAdapter<VolumeDesignCategory, VolumeDesignData> adapter)
        : base(adapter) { }
}
