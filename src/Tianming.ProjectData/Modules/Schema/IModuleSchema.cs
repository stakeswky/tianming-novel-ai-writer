using System.Collections.Generic;
using TM.Framework.Common.Models;

namespace TM.Services.Modules.ProjectData.Modules.Schema;

/// <summary>
/// 模块 schema —— 描述一个 Category/Data 三元组的元数据。
/// 供 ModuleDataAdapter 和 DataManagementViewModel 使用，是 M4.1+ 页面统一渲染的源头。
/// </summary>
public interface IModuleSchema<TCategory, TData>
    where TCategory : class, ICategory
    where TData : class, IDataItem
{
    /// <summary>页面标题，例如"世界观规则"。</summary>
    string PageTitle { get; }

    /// <summary>页面图标（emoji 或 Lucide 名）。</summary>
    string PageIcon { get; }

    /// <summary>
    /// 相对项目根目录的模块路径，如 "Design/GlobalSettings/WorldRules"。
    /// FileModuleDataStore 会在此路径下读写 categories.json / built_in_categories.json / data.json。
    /// </summary>
    string ModuleRelativePath { get; }

    /// <summary>字段描述（驱动 DataFormView 动态渲染）。</summary>
    IReadOnlyList<FieldDescriptor> Fields { get; }

    /// <summary>创建带默认值的空白数据项。</summary>
    TData CreateNewItem();

    /// <summary>创建带默认值的空白分类。</summary>
    TCategory CreateNewCategory(string name);

    /// <summary>
    /// 为 AI 批量生成构造上下文文本（M4.1/M4.5 之后接入；M4.0 留接口）。
    /// </summary>
    string BuildAIPromptContext(IReadOnlyList<TData> existing);
}
