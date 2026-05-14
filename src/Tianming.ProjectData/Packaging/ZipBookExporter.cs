using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging;

public sealed class ZipBookExporter : IBookExporter
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".staged",
        ".staging",
        ".wal",
        ".backups",
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    public Task ExportAsync(string projectRoot, string outputZipPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("项目根目录不能为空", nameof(projectRoot));
        if (string.IsNullOrWhiteSpace(outputZipPath))
            throw new ArgumentException("输出 ZIP 路径不能为空", nameof(outputZipPath));

        var outputDirectory = Path.GetDirectoryName(outputZipPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (File.Exists(outputZipPath))
            File.Delete(outputZipPath);

        var outputFullPath = Path.GetFullPath(outputZipPath);
        using var stream = File.Create(outputZipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var file in EnumerateFiles(projectRoot, outputFullPath, ct))
        {
            var relativePath = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<string> EnumerateFiles(string root, string outputFullPath, CancellationToken ct)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            if (string.Equals(Path.GetFullPath(file), outputFullPath, StringComparison.OrdinalIgnoreCase))
                continue;

            yield return file;
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var directoryName = Path.GetFileName(directory);
            if (ExcludedDirectories.Contains(directoryName))
                continue;

            foreach (var nestedFile in EnumerateFilesRecursive(directory, ct))
                yield return nestedFile;
        }
    }

    private static IEnumerable<string> EnumerateFilesRecursive(string directory, CancellationToken ct)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            yield return file;
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var directoryName = Path.GetFileName(childDirectory);
            if (ExcludedDirectories.Contains(directoryName))
                continue;

            foreach (var nestedFile in EnumerateFilesRecursive(childDirectory, ct))
                yield return nestedFile;
        }
    }
}
