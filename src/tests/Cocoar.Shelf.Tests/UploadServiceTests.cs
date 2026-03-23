using System.IO.Compression;
using Cocoar.Shelf.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cocoar.Shelf.Tests;

public sealed class UploadServiceTests : IDisposable
{
    private readonly string _docsRoot;
    private readonly UploadService _sut;

    public UploadServiceTests()
    {
        _docsRoot = Path.Combine(Path.GetTempPath(), $"shelf-upload-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_docsRoot);

        var config = new TestReactiveConfig<ShelfOptions>(new ShelfOptions { DocsRoot = _docsRoot });
        _sut = new UploadService(config, NullLogger<UploadService>.Instance, new BasePathDetector());
    }

    [Fact]
    public async Task UploadVersion_ExtractsZipToCorrectLocation()
    {
        using var zip = CreateZip(("index.html", "<html></html>"), ("assets/style.css", "body{}"));

        var result = await _sut.UploadVersionAsync("myproduct", "v1", zip);

        Assert.Equal(UploadStatus.Success, result.Status);
        Assert.True(File.Exists(Path.Combine(_docsRoot, "myproduct", "v1", "index.html")));
        Assert.True(File.Exists(Path.Combine(_docsRoot, "myproduct", "v1", "assets", "style.css")));
    }

    [Fact]
    public async Task UploadVersion_ReturnsMissingIndexHtml_WhenNoIndexHtml()
    {
        using var zip = CreateZip(("readme.txt", "hello"));

        var result = await _sut.UploadVersionAsync("myproduct", "v1", zip);

        Assert.Equal(UploadStatus.MissingIndexHtml, result.Status);
    }

    [Fact]
    public async Task UploadVersion_DetectsZipSlipAttack()
    {
        using var zip = CreateZipWithEntry("../../../etc/passwd", "pwned");

        var result = await _sut.UploadVersionAsync("myproduct", "v1", zip);

        Assert.Equal(UploadStatus.InvalidArchive, result.Status);
        Assert.Contains("path traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadVersion_ReturnsInvalidArchive_WhenNotZip()
    {
        using var stream = new MemoryStream("not a zip file"u8.ToArray());

        var result = await _sut.UploadVersionAsync("myproduct", "v1", stream);

        Assert.Equal(UploadStatus.InvalidArchive, result.Status);
    }

    [Fact]
    public async Task UploadVersion_CleansUpTempDirOnFailure()
    {
        using var stream = new MemoryStream("not a zip file"u8.ToArray());

        await _sut.UploadVersionAsync("myproduct", "v1", stream);

        var tempDir = Path.Combine(_docsRoot, ".shelf-tmp");
        if (Directory.Exists(tempDir))
        {
            var remaining = Directory.GetDirectories(tempDir);
            Assert.Empty(remaining);
        }
    }

    [Fact]
    public async Task UploadVersion_ReplacesExistingVersion()
    {
        using var zip1 = CreateZip(("index.html", "<html>v1</html>"));
        await _sut.UploadVersionAsync("myproduct", "v1", zip1);

        using var zip2 = CreateZip(("index.html", "<html>v1-updated</html>"));
        var result = await _sut.UploadVersionAsync("myproduct", "v1", zip2);

        Assert.Equal(UploadStatus.Success, result.Status);
        var content = File.ReadAllText(Path.Combine(_docsRoot, "myproduct", "v1", "index.html"));
        Assert.Equal("<html>v1-updated</html>", content);
    }

    [Fact]
    public async Task UploadVersion_CreatesProductDirectoryIfMissing()
    {
        using var zip = CreateZip(("index.html", "<html></html>"));

        var result = await _sut.UploadVersionAsync("newproduct", "v1", zip);

        Assert.Equal(UploadStatus.Success, result.Status);
        Assert.True(Directory.Exists(Path.Combine(_docsRoot, "newproduct", "v1")));
    }

    private static MemoryStream CreateZip(params (string name, string content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateZipWithEntry(string entryPath, string content)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
        ms.Position = 0;
        return ms;
    }

    public void Dispose()
    {
        try { Directory.Delete(_docsRoot, recursive: true); }
        catch { /* cleanup best-effort */ }
    }
}
