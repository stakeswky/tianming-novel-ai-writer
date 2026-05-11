namespace TM.Framework.Appearance;

public sealed class PortableThemeImportResult
{
    public int ImportedCount { get; init; }

    public int SkippedCount { get; init; }
}

public sealed class PortableThemeExportResult
{
    public bool Success { get; init; }

    public string? FileName { get; init; }

    public string? FilePath { get; init; }

    public string? ErrorMessage { get; init; }
}

public sealed class PortableThemeBulkExportResult
{
    public int ExportedCount { get; init; }

    public string? FolderName { get; init; }

    public string? FolderPath { get; init; }
}

public sealed class PortableExportedThemeItem
{
    public string FileName { get; init; } = string.Empty;

    public DateTime ExportTime { get; init; }

    public string FileSize { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;
}

public sealed class PortableThemeImportExportService
{
    private readonly string _themesPath;
    private readonly string _exportPath;
    private readonly Func<DateTime> _clock;

    public PortableThemeImportExportService(
        string themesPath,
        string exportPath,
        Func<DateTime>? clock = null)
    {
        _themesPath = string.IsNullOrWhiteSpace(themesPath)
            ? throw new ArgumentException("Themes path is required.", nameof(themesPath))
            : themesPath;
        _exportPath = string.IsNullOrWhiteSpace(exportPath)
            ? throw new ArgumentException("Export path is required.", nameof(exportPath))
            : exportPath;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableThemeImportResult> ImportThemesAsync(
        IReadOnlyList<string> sourceFiles,
        Func<string, bool>? overwriteExisting = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_themesPath);

        var imported = 0;
        var skipped = 0;
        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await ValidateThemeFileAsync(sourceFile, cancellationToken).ConfigureAwait(false))
            {
                skipped++;
                continue;
            }

            var fileName = Path.GetFileName(sourceFile);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                skipped++;
                continue;
            }

            var destinationPath = Path.Combine(_themesPath, fileName);
            if (File.Exists(destinationPath) && overwriteExisting?.Invoke(fileName) != true)
            {
                skipped++;
                continue;
            }

            File.Copy(sourceFile, destinationPath, overwrite: true);
            imported++;
        }

        return new PortableThemeImportResult
        {
            ImportedCount = imported,
            SkippedCount = skipped
        };
    }

    public async Task<PortableThemeExportResult> ExportCurrentThemeAsync(
        PortableThemeType currentTheme,
        CancellationToken cancellationToken = default)
    {
        var themeFileName = GetThemeFileName(currentTheme);
        if (string.IsNullOrWhiteSpace(themeFileName))
        {
            return new PortableThemeExportResult
            {
                Success = false,
                ErrorMessage = "无法导出当前主题"
            };
        }

        var sourcePath = Path.Combine(_themesPath, themeFileName);
        if (!File.Exists(sourcePath))
        {
            return new PortableThemeExportResult
            {
                Success = false,
                ErrorMessage = $"主题文件不存在：{themeFileName}"
            };
        }

        Directory.CreateDirectory(_exportPath);
        var exportFileName = $"{Path.GetFileNameWithoutExtension(themeFileName)}_{_clock():yyyyMMdd_HHmmss}.xaml";
        var exportFilePath = Path.Combine(_exportPath, exportFileName);

        await CopyFileAsync(sourcePath, exportFilePath, cancellationToken).ConfigureAwait(false);

        return new PortableThemeExportResult
        {
            Success = true,
            FileName = exportFileName,
            FilePath = exportFilePath
        };
    }

    public async Task<PortableThemeBulkExportResult> ExportAllThemesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_themesPath))
        {
            return new PortableThemeBulkExportResult();
        }

        var themeFiles = Directory.GetFiles(_themesPath, "*.xaml");
        if (themeFiles.Length == 0)
        {
            return new PortableThemeBulkExportResult();
        }

        Directory.CreateDirectory(_exportPath);
        var folderName = $"所有主题_{_clock():yyyyMMdd_HHmmss}";
        var folderPath = Path.Combine(_exportPath, folderName);
        Directory.CreateDirectory(folderPath);

        var exported = 0;
        foreach (var themeFile in themeFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = Path.Combine(folderPath, Path.GetFileName(themeFile));
            await CopyFileAsync(themeFile, destinationPath, cancellationToken).ConfigureAwait(false);
            exported++;
        }

        return new PortableThemeBulkExportResult
        {
            ExportedCount = exported,
            FolderName = folderName,
            FolderPath = folderPath
        };
    }

    public Task<IReadOnlyList<PortableExportedThemeItem>> ListExportedThemesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_exportPath))
        {
            return Task.FromResult<IReadOnlyList<PortableExportedThemeItem>>([]);
        }

        var items = Directory.GetFiles(_exportPath, "*.xaml", SearchOption.AllDirectories)
            .OrderByDescending(File.GetCreationTime)
            .Take(20)
            .Select(file =>
            {
                var fileInfo = new FileInfo(file);
                return new PortableExportedThemeItem
                {
                    FileName = Path.GetRelativePath(_exportPath, file),
                    ExportTime = fileInfo.CreationTime,
                    FileSize = FormatFileSize(fileInfo.Length),
                    FullPath = file
                };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<PortableExportedThemeItem>>(items);
    }

    public static string? GetThemeFileName(PortableThemeType theme)
    {
        return theme switch
        {
            PortableThemeType.Light => "LightTheme.xaml",
            PortableThemeType.Dark => "DarkTheme.xaml",
            PortableThemeType.Green => "GreenTheme.xaml",
            PortableThemeType.Business => "BusinessTheme.xaml",
            PortableThemeType.ModernBlue => "ModernBlueTheme.xaml",
            PortableThemeType.Violet => "VioletTheme.xaml",
            PortableThemeType.WarmOrange => "WarmOrangeTheme.xaml",
            PortableThemeType.Pink => "PinkTheme.xaml",
            PortableThemeType.TechCyan => "TechCyanTheme.xaml",
            PortableThemeType.MinimalBlack => "MinimalBlackTheme.xaml",
            PortableThemeType.Arctic => "ArcticTheme.xaml",
            PortableThemeType.Forest => "ForestTheme.xaml",
            PortableThemeType.Sunset => "SunsetTheme.xaml",
            PortableThemeType.Morandi => "MorandiTheme.xaml",
            PortableThemeType.HighContrast => "HighContrastTheme.xaml",
            _ => null
        };
    }

    public static async Task<bool> ValidateThemeFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (!content.Contains("<ResourceDictionary", StringComparison.Ordinal)
                || !content.Contains("xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"", StringComparison.Ordinal))
            {
                return false;
            }

            return content.Contains("PrimaryColor", StringComparison.Ordinal)
                || content.Contains("ContentBackground", StringComparison.Ordinal)
                || content.Contains("TextPrimary", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var length = (double)bytes;
        var order = 0;
        while (length >= 1024 && order < sizes.Length - 1)
        {
            order++;
            length /= 1024;
        }

        return $"{length:0.##} {sizes[order]}";
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = File.OpenRead(sourcePath);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
    }
}
