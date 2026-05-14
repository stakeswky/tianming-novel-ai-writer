namespace TM.Services.Modules.ProjectData.Humanize
{
    public sealed class HumanizeContext
    {
        public string ChapterId { get; set; } = string.Empty;

        public string InputText { get; set; } = string.Empty;

        public string? GenreHint { get; set; }
    }
}
