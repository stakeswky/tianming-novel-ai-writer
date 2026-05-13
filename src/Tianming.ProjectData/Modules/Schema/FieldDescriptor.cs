using System.Collections.Generic;

namespace TM.Services.Modules.ProjectData.Modules.Schema;

public enum FieldType
{
    SingleLineText,
    MultiLineText,
    Number,
    Tags,        // List<string>，逗号/空格分隔输入
    Enum,
    Boolean
}

public sealed record FieldDescriptor(
    string PropertyName,
    string Label,
    FieldType Type,
    bool Required,
    string? Placeholder,
    int? MaxLength = null,
    IReadOnlyList<string>? EnumOptions = null);
