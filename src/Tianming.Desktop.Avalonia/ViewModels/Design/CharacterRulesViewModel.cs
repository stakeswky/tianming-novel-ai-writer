using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class CharacterRulesViewModel
    : DataManagementViewModel<CharacterRulesCategory, CharacterRulesData, CharacterRulesSchema>
{
    public CharacterRulesViewModel(ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData> adapter)
        : base(adapter) { }
}
