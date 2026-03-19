using System.Collections.Concurrent;
using System.IO.Compression;
using Microsoft.Extensions.Options;

namespace Cocoar.Shelf.Services;

public sealed partial class UploadService : IUploadService
{
    private readonly ShelfOptions _options;
    private readonly ILogger<UploadService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public UploadService(IOptions<ShelfOptions> options, ILogger<UploadService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<UploadResult> UploadVersionAsync(string product, string version, Stream zipStream, CancellationToken ct = default)
    {
        var key = $"{product}/{version}";
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        if (!await semaphore.WaitAsync(0, ct))
            return new UploadResult(UploadStatus.VersionConflict, "Upload for this version is already in progress");

        var tempDir = Path.Combine(_options.DocsRoot, ".shelf-tmp", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            var tempDirFull = Path.GetFullPath(tempDir);

            // Extract ZIP
            try
            {
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    var destPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));

                    // ZIP-Slip protection
                    if (!destPath.StartsWith(tempDirFull, StringComparison.OrdinalIgnoreCase))
                        return new UploadResult(UploadStatus.InvalidArchive, "Archive contains path traversal entries");

                    var destDir = Path.GetDirectoryName(destPath);
                    if (destDir != null)
                        Directory.CreateDirectory(destDir);

                    entry.ExtractToFile(destPath, overwrite: true);
                }
            }
            catch (InvalidDataException)
            {
                return new UploadResult(UploadStatus.InvalidArchive, "Invalid or corrupt ZIP archive");
            }

            // Validate: index.html must exist at the root
            if (!File.Exists(Path.Combine(tempDir, "index.html")))
                return new UploadResult(UploadStatus.MissingIndexHtml, "Archive must contain an index.html at the root");

            // Ensure product directory exists
            var productDir = Path.Combine(_options.DocsRoot, product);
            Directory.CreateDirectory(productDir);

            // Atomic move: swap existing version if present
            var destVersionDir = Path.Combine(_options.DocsRoot, product, version);

            if (Directory.Exists(destVersionDir))
            {
                var oldDir = Path.Combine(_options.DocsRoot, ".shelf-tmp", $"old-{Guid.NewGuid():N}");
                Directory.Move(destVersionDir, oldDir);

                try { Directory.Delete(oldDir, recursive: true); }
                catch { /* best-effort cleanup */ }
            }

            Directory.Move(tempDir, destVersionDir);

            LogVersionDeployed(product, version);
            return new UploadResult(UploadStatus.Success);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUploadFailed(product, version, ex.Message);
            return new UploadResult(UploadStatus.InvalidArchive, $"Upload failed: {ex.Message}");
        }
        finally
        {
            semaphore.Release();

            // Clean up temp dir if it still exists (failure path)
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Version deployed: {Product}/{Version}")]
    private partial void LogVersionDeployed(string product, string version);

    [LoggerMessage(Level = LogLevel.Error, Message = "Upload failed for {Product}/{Version}: {Error}")]
    private partial void LogUploadFailed(string product, string version, string error);
}
