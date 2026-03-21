using Cocoar.Shelf.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cocoar.Shelf.Tests;

public sealed class UploadServiceDeleteTests : IDisposable
{
    private readonly string _docsRoot;
    private readonly UploadService _sut;

    public UploadServiceDeleteTests()
    {
        _docsRoot = Path.Combine(Path.GetTempPath(), $"shelf-delete-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_docsRoot);

        var config = new TestReactiveConfig<ShelfOptions>(new ShelfOptions { DocsRoot = _docsRoot });
        _sut = new UploadService(config, NullLogger<UploadService>.Instance);
    }

    [Fact]
    public async Task DeleteVersion_RemovesDirectory_ReturnsTrue()
    {
        var versionDir = Path.Combine(_docsRoot, "myproduct", "v1");
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "index.html"), "<html></html>");

        var result = await _sut.DeleteVersionAsync("myproduct", "v1");

        Assert.True(result);
        Assert.False(Directory.Exists(versionDir));
    }

    [Fact]
    public async Task DeleteVersion_ReturnsFalse_WhenNotFound()
    {
        var result = await _sut.DeleteVersionAsync("myproduct", "v99");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteVersion_PreservesOtherVersions()
    {
        Directory.CreateDirectory(Path.Combine(_docsRoot, "myproduct", "v1"));
        Directory.CreateDirectory(Path.Combine(_docsRoot, "myproduct", "v2"));

        await _sut.DeleteVersionAsync("myproduct", "v1");

        Assert.False(Directory.Exists(Path.Combine(_docsRoot, "myproduct", "v1")));
        Assert.True(Directory.Exists(Path.Combine(_docsRoot, "myproduct", "v2")));
    }

    [Fact]
    public async Task DeleteVersion_RemovesAllContents()
    {
        var versionDir = Path.Combine(_docsRoot, "myproduct", "v3");
        Directory.CreateDirectory(Path.Combine(versionDir, "assets"));
        Directory.CreateDirectory(Path.Combine(versionDir, "guide"));
        File.WriteAllText(Path.Combine(versionDir, "index.html"), "<html></html>");
        File.WriteAllText(Path.Combine(versionDir, "assets", "style.css"), "body{}");
        File.WriteAllText(Path.Combine(versionDir, "guide", "page.html"), "<html></html>");

        var result = await _sut.DeleteVersionAsync("myproduct", "v3");

        Assert.True(result);
        Assert.False(Directory.Exists(versionDir));
    }

    public void Dispose()
    {
        try { Directory.Delete(_docsRoot, recursive: true); }
        catch { /* best-effort */ }
    }
}
