namespace TM.Framework.Notifications;

public sealed record PortableSoundLibraryItem(
    string Name,
    string FilePath,
    long SizeBytes,
    string Size,
    string Duration,
    string Category);

public sealed record PortableSoundLibraryOpenFolderPlan(
    string FileName,
    IReadOnlyList<string> Arguments);

public sealed class PortableSoundLibrary
{
    private const string MacOSOpenToolPath = "/usr/bin/open";
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav",
        ".mp3"
    };

    private readonly string _libraryDirectory;

    public PortableSoundLibrary(string libraryDirectory)
    {
        _libraryDirectory = string.IsNullOrWhiteSpace(libraryDirectory)
            ? throw new ArgumentException("Sound library directory is required.", nameof(libraryDirectory))
            : Path.GetFullPath(libraryDirectory);
    }

    public IReadOnlyList<PortableSoundLibraryItem> ListSounds()
    {
        if (!Directory.Exists(_libraryDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_libraryDirectory)
            .Where(IsSupportedSoundFile)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(CreateItem)
            .ToList();
    }

    public async Task<PortableSoundLibraryItem> ImportSoundAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Sound file path is required.", nameof(sourceFilePath));
        }

        var fullSourcePath = Path.GetFullPath(sourceFilePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("音效文件不存在", fullSourcePath);
        }

        if (!IsSupportedExtension(fullSourcePath))
        {
            throw new InvalidOperationException("仅支持 WAV 和 MP3 音效文件");
        }

        Directory.CreateDirectory(_libraryDirectory);
        var targetPath = GetAvailableTargetPath(Path.GetFileName(fullSourcePath));
        await using (var source = File.OpenRead(fullSourcePath))
        await using (var target = File.Create(targetPath))
        {
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }

        return CreateItem(targetPath);
    }

    public bool DeleteSound(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(filePath);
        EnsureInsideLibrary(fullPath);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        File.Delete(fullPath);
        return true;
    }

    public PortableSoundLibraryOpenFolderPlan CreateOpenFolderPlan()
    {
        Directory.CreateDirectory(_libraryDirectory);
        return new PortableSoundLibraryOpenFolderPlan(MacOSOpenToolPath, [_libraryDirectory]);
    }

    private static bool IsSupportedSoundFile(string path)
    {
        return File.Exists(path) && IsSupportedExtension(path);
    }

    private static bool IsSupportedExtension(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    private PortableSoundLibraryItem CreateItem(string path)
    {
        var fileInfo = new FileInfo(path);
        return new PortableSoundLibraryItem(
            fileInfo.Name,
            fileInfo.FullName,
            fileInfo.Length,
            FormatFileSize(fileInfo.Length),
            "0:00",
            GetCategory(fileInfo.Extension));
    }

    private string GetAvailableTargetPath(string fileName)
    {
        var candidate = Path.Combine(_libraryDirectory, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 1; ; index++)
        {
            candidate = Path.Combine(_libraryDirectory, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private void EnsureInsideLibrary(string filePath)
    {
        var relativePath = Path.GetRelativePath(_libraryDirectory, filePath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("音效文件必须位于音效库目录内");
        }
    }

    private static string GetCategory(string extension)
    {
        return extension.TrimStart('.').ToUpperInvariant();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F2} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
