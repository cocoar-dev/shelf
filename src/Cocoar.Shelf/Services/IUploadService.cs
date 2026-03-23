namespace Cocoar.Shelf.Services;

public interface IUploadService
{
    Task<UploadResult> UploadVersionAsync(string product, string version, Stream zipStream, CancellationToken ct = default);

    Task<bool> DeleteVersionAsync(string product, string version, CancellationToken ct = default);

    Task<bool> DeleteProductDataAsync(string product, CancellationToken ct = default);
}

public enum UploadStatus
{
    Success,
    InvalidArchive,
    MissingIndexHtml,
    VersionConflict
}

public record UploadResult(UploadStatus Status, string? Error = null);
