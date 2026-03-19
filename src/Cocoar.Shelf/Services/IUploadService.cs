namespace Cocoar.Shelf.Services;

public interface IUploadService
{
    Task<UploadResult> UploadVersionAsync(string product, string version, Stream zipStream, CancellationToken ct = default);
}

public enum UploadStatus
{
    Success,
    InvalidArchive,
    MissingIndexHtml,
    VersionConflict
}

public record UploadResult(UploadStatus Status, string? Error = null);
