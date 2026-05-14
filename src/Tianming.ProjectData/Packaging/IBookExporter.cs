using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging;

public interface IBookExporter
{
    Task ExportAsync(string projectRoot, string outputZipPath, CancellationToken ct = default);
}
