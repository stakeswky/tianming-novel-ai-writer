namespace TM.Framework.Appearance;

public sealed class PortableEditorFontPreset
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public PortableFontSettings Settings { get; set; } = new();

    public bool IsInstalled { get; set; }

    public string DownloadUrl { get; set; } = string.Empty;

    public List<string> Features { get; set; } = [];
}

public sealed class PortableEditorFontPresetService
{
    private readonly List<PortableEditorFontPreset> _builtInPresets;
    private readonly HashSet<string> _installedFonts;

    public PortableEditorFontPresetService(IEnumerable<string> installedFonts)
    {
        _installedFonts = installedFonts
            .Where(fontName => !string.IsNullOrWhiteSpace(fontName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _builtInPresets = InitializeBuiltInPresets();
        UpdateInstallationStatus();
    }

    public List<PortableEditorFontPreset> GetBuiltInPresets()
    {
        UpdateInstallationStatus();
        return _builtInPresets.ToList();
    }

    public bool IsFontInstalled(string fontName)
    {
        return !string.IsNullOrWhiteSpace(fontName) && _installedFonts.Contains(fontName);
    }

    private void UpdateInstallationStatus()
    {
        foreach (var preset in _builtInPresets)
        {
            preset.IsInstalled = IsFontInstalled(preset.Settings.FontFamily);
        }
    }

    private static List<PortableEditorFontPreset> InitializeBuiltInPresets()
    {
        return
        [
            new PortableEditorFontPreset
            {
                Name = "Consolas（Windows经典）",
                Description = "微软经典等宽字体，清晰易读，Windows内置",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Consolas",
                    FontSize = 12,
                    FontWeight = "Normal",
                    LineHeight = 1.5,
                    LetterSpacing = 0,
                    EnableLigatures = false
                },
                DownloadUrl = "https://docs.microsoft.com/typography/font-list/consolas",
                Features = ["等宽", "清晰", "内置"]
            },

            new PortableEditorFontPreset
            {
                Name = "Fira Code（连字推荐）",
                Description = "Mozilla开发的编程字体，出色的连字支持",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Fira Code",
                    FontSize = 11,
                    FontWeight = "Normal",
                    LineHeight = 1.6,
                    LetterSpacing = 0,
                    EnableLigatures = true
                },
                DownloadUrl = "https://github.com/tonsky/FiraCode",
                Features = ["等宽", "连字", "开源"]
            },

            new PortableEditorFontPreset
            {
                Name = "JetBrains Mono（专业）",
                Description = "JetBrains出品，专为开发者设计，连字美观",
                Settings = new PortableFontSettings
                {
                    FontFamily = "JetBrains Mono",
                    FontSize = 12,
                    FontWeight = "Normal",
                    LineHeight = 1.5,
                    LetterSpacing = 0,
                    EnableLigatures = true
                },
                DownloadUrl = "https://www.jetbrains.com/lp/mono/",
                Features = ["等宽", "连字", "专业"]
            },

            new PortableEditorFontPreset
            {
                Name = "Cascadia Code（现代）",
                Description = "微软现代终端字体，支持连字和多种样式集",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Cascadia Code",
                    FontSize = 11,
                    FontWeight = "Normal",
                    LineHeight = 1.5,
                    LetterSpacing = 0,
                    EnableLigatures = true
                },
                DownloadUrl = "https://github.com/microsoft/cascadia-code",
                Features = ["等宽", "连字", "现代", "OpenType"]
            },

            new PortableEditorFontPreset
            {
                Name = "Source Code Pro（Adobe）",
                Description = "Adobe开源字体，极佳的可读性",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Source Code Pro",
                    FontSize = 11,
                    FontWeight = "Normal",
                    LineHeight = 1.6,
                    LetterSpacing = 0,
                    EnableLigatures = false
                },
                DownloadUrl = "https://github.com/adobe-fonts/source-code-pro",
                Features = ["等宽", "清晰", "开源"]
            },

            new PortableEditorFontPreset
            {
                Name = "Inconsolata（紧凑）",
                Description = "紧凑型等宽字体，适合小屏幕",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Inconsolata",
                    FontSize = 12,
                    FontWeight = "Normal",
                    LineHeight = 1.5,
                    LetterSpacing = 0,
                    EnableLigatures = false
                },
                DownloadUrl = "https://fonts.google.com/specimen/Inconsolata",
                Features = ["等宽", "紧凑", "Google Fonts"]
            },

            new PortableEditorFontPreset
            {
                Name = "Monaco（macOS风格）",
                Description = "macOS经典等宽字体",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Monaco",
                    FontSize = 11,
                    FontWeight = "Normal",
                    LineHeight = 1.5,
                    LetterSpacing = 0,
                    EnableLigatures = false
                },
                DownloadUrl = "https://en.wikipedia.org/wiki/Monaco_(typeface)",
                Features = ["等宽", "macOS"]
            },

            new PortableEditorFontPreset
            {
                Name = "Courier New（经典）",
                Description = "传统打字机风格，广泛支持",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Courier New",
                    FontSize = 12,
                    FontWeight = "Normal",
                    LineHeight = 1.6,
                    LetterSpacing = 0,
                    EnableLigatures = false
                },
                DownloadUrl = string.Empty,
                Features = ["等宽", "经典", "内置"]
            },

            new PortableEditorFontPreset
            {
                Name = "Lucida Console（内置）",
                Description = "Windows内置，简洁清晰",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Lucida Console",
                    FontSize = 11,
                    FontWeight = "Normal",
                    LineHeight = 1.5,
                    LetterSpacing = 0,
                    EnableLigatures = false
                },
                DownloadUrl = string.Empty,
                Features = ["等宽", "内置"]
            }
        ];
    }
}
