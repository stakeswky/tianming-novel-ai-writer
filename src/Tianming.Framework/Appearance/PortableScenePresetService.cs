namespace TM.Framework.Appearance;

public enum PortableUsageScene
{
    Coding,
    Reading,
    Presentation,
    Terminal,
    Documentation
}

public sealed class PortableScenePreset
{
    public PortableUsageScene Scene { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public PortableFontSettings Settings { get; set; } = new();

    public List<string> RecommendedFonts { get; set; } = [];

    public List<string> Tips { get; set; } = [];

    public bool IsSelected { get; set; }
}

public sealed class PortableScenePresetService
{
    private readonly List<PortableScenePreset> _scenePresets = InitializeScenePresets();

    public List<PortableScenePreset> GetAllPresets()
    {
        return _scenePresets.ToList();
    }

    public PortableScenePreset? GetPreset(PortableUsageScene scene)
    {
        return _scenePresets.FirstOrDefault(preset => preset.Scene == scene);
    }

    private static List<PortableScenePreset> InitializeScenePresets()
    {
        return
        [
            new PortableScenePreset
            {
                Scene = PortableUsageScene.Coding,
                Name = "编码开发",
                Description = "专为代码编写优化，强调清晰度和可读性",
                Icon = "💻",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Consolas",
                    FontSize = 12,
                    FontWeight = "Normal",
                    LineHeight = 1.5,
                    LetterSpacing = 0,
                    EnableLigatures = true,
                    VisualizeWhitespace = true,
                    TabSymbol = "→",
                    SpaceSymbol = "·"
                },
                RecommendedFonts =
                [
                    "Consolas", "Fira Code", "JetBrains Mono",
                    "Cascadia Code", "Source Code Pro"
                ],
                Tips =
                [
                    "建议启用连字以改善运算符显示",
                    "使用等宽字体保证代码对齐",
                    "字号12-14px最适合长时间编码",
                    "行高1.5-1.6倍提供舒适的垂直间距"
                ]
            },

            new PortableScenePreset
            {
                Scene = PortableUsageScene.Reading,
                Name = "文档阅读",
                Description = "舒适的阅读体验，减少眼睛疲劳",
                Icon = "📖",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Consolas",
                    FontSize = 13,
                    FontWeight = "Normal",
                    LineHeight = 1.8,
                    LetterSpacing = 0.5,
                    EnableLigatures = false,
                    VisualizeWhitespace = false,
                    TabSymbol = "→",
                    SpaceSymbol = "·"
                },
                RecommendedFonts =
                [
                    "Consolas", "Georgia", "Times New Roman",
                    "Source Code Pro", "Inconsolata"
                ],
                Tips =
                [
                    "较大的字号和行高提升阅读舒适度",
                    "适当的字间距减少视觉拥挤感",
                    "关闭空白符可视化以减少干扰",
                    "衬线字体更适合长文档阅读"
                ]
            },

            new PortableScenePreset
            {
                Scene = PortableUsageScene.Presentation,
                Name = "屏幕演示",
                Description = "大字号，高对比度，适合远距离观看",
                Icon = "📺",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Consolas",
                    FontSize = 18,
                    FontWeight = "SemiBold",
                    LineHeight = 2.0,
                    LetterSpacing = 1,
                    EnableLigatures = false,
                    VisualizeWhitespace = false,
                    TabSymbol = "→",
                    SpaceSymbol = "·"
                },
                RecommendedFonts =
                [
                    "Consolas", "Arial", "Verdana",
                    "Cascadia Code", "JetBrains Mono"
                ],
                Tips =
                [
                    "使用18-24px字号确保远处可读",
                    "加粗字体增强显示效果",
                    "2倍行高提供充足的视觉空间",
                    "简洁的无衬线字体更易识别"
                ]
            },

            new PortableScenePreset
            {
                Scene = PortableUsageScene.Terminal,
                Name = "终端控制台",
                Description = "紧凑布局，适合命令行和日志查看",
                Icon = "⌨️",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Consolas",
                    FontSize = 11,
                    FontWeight = "Normal",
                    LineHeight = 1.3,
                    LetterSpacing = 0,
                    EnableLigatures = false,
                    VisualizeWhitespace = false,
                    TabSymbol = "→",
                    SpaceSymbol = "·"
                },
                RecommendedFonts =
                [
                    "Consolas", "Cascadia Mono", "Monaco",
                    "Lucida Console", "Courier New"
                ],
                Tips =
                [
                    "紧凑的字号和行高显示更多内容",
                    "等宽字体确保表格和输出对齐",
                    "避免连字以保持原始字符显示",
                    "字号10-11px适合高密度信息显示"
                ]
            },

            new PortableScenePreset
            {
                Scene = PortableUsageScene.Documentation,
                Name = "技术文档",
                Description = "平衡代码和文本的混合阅读",
                Icon = "📝",
                Settings = new PortableFontSettings
                {
                    FontFamily = "Consolas",
                    FontSize = 12,
                    FontWeight = "Normal",
                    LineHeight = 1.6,
                    LetterSpacing = 0.3,
                    EnableLigatures = true,
                    VisualizeWhitespace = false,
                    TabSymbol = "→",
                    SpaceSymbol = "·"
                },
                RecommendedFonts =
                [
                    "Consolas", "Source Code Pro", "Georgia",
                    "Inconsolata", "JetBrains Mono"
                ],
                Tips =
                [
                    "中等字号兼顾代码和文本阅读",
                    "适度的行高和字间距提升可读性",
                    "连字改善代码示例的显示效果",
                    "等宽字体统一代码和文本风格"
                ]
            }
        ];
    }
}
