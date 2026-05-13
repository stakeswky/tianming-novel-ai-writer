using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels;

// 给基类追加 RelayCommand 包装：M4.1+ view 通过 {Binding XxxCommand} 绑定。
// 不动 M4.0 接口，仅扩展（partial 同名类拆文件）。
public abstract partial class DataManagementViewModel<TCategory, TData, TSchema>
{
    [RelayCommand]
    private Task DeleteSelectedItem() => DeleteSelectedItemAsync();

    [RelayCommand]
    private Task UpdateSelectedItem() => UpdateSelectedItemAsync();

    [RelayCommand]
    private async Task AddNewItemInCurrentCategory()
    {
        if (SelectedCategory == null) return;
        var created = await AddNewItemAsync(categoryName: SelectedCategory.Name, name: "新条目").ConfigureAwait(false);
        if (created != null) SelectedItem = created;
    }

    [RelayCommand]
    private async Task AddCategoryWithDefaultName()
    {
        // UI 暂时不弹输入框 — 用 "新分类 N" 占位（M4.1 个人自用可接受，M4 后续接对话框）
        var n = Categories.Count + 1;
        await AddCategoryAsync($"新分类 {n}").ConfigureAwait(false);
    }
}
