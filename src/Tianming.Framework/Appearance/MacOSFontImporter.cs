namespace TM.Framework.Appearance;

public enum MacOSFontImportFailureReason
{
    None,
    SourceMissing,
    UnsupportedExtension,
    CopyFailed
}

public sealed class MacOSFontImportResult
{
    public bool Imported { get; init; }

    public string SourcePath { get; init; } = string.Empty;

    public string DestinationPath { get; init; } = string.Empty;

    public string FontFamilyName { get; init; } = string.Empty;

    public MacOSFontImportFailureReason FailureReason { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class MacOSFontImporter
{
    private static readonly string[] SupportedExtensions = [".ttf", ".ttc", ".otf"];

    private readonly string _targetDirectory;

    public MacOSFontImporter()
        : this(GetDefaultUserFontDirectory())
    {
    }

    public MacOSFontImporter(string targetDirectory)
    {
        _targetDirectory = string.IsNullOrWhiteSpace(targetDirectory)
            ? throw new ArgumentException("Font import target directory is required.", nameof(targetDirectory))
            : targetDirectory;
    }

    public async Task<MacOSFontImportResult> ImportAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            return Failed(sourcePath, MacOSFontImportFailureReason.SourceMissing);
        }

        if (!IsSupportedFontFile(sourcePath))
        {
            return Failed(sourcePath, MacOSFontImportFailureReason.UnsupportedExtension);
        }

        try
        {
            Directory.CreateDirectory(_targetDirectory);
            var destinationPath = ResolveDestinationPath(sourcePath);
            await using var source = File.OpenRead(sourcePath);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            return new MacOSFontImportResult
            {
                Imported = true,
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                FontFamilyName = MacOSFontCatalog.NormalizeFontFamilyName(Path.GetFileName(sourcePath)),
                FailureReason = MacOSFontImportFailureReason.None
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new MacOSFontImportResult
            {
                Imported = false,
                SourcePath = sourcePath,
                FailureReason = MacOSFontImportFailureReason.CopyFailed,
                ErrorMessage = ex.Message
            };
        }
    }

    private string ResolveDestinationPath(string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        var candidate = Path.Combine(_targetDirectory, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 1; ; suffix++)
        {
            candidate = Path.Combine(_targetDirectory, $"{stem} {suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool IsSupportedFontFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedExtensions.Any(supported => string.Equals(supported, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static MacOSFontImportResult Failed(string sourcePath, MacOSFontImportFailureReason reason)
    {
        return new MacOSFontImportResult
        {
            Imported = false,
            SourcePath = sourcePath,
            FailureReason = reason
        };
    }

    private static string GetDefaultUserFontDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home)
            ? Path.Combine("Library", "Fonts")
            : Path.Combine(home, "Library", "Fonts");
    }
}
