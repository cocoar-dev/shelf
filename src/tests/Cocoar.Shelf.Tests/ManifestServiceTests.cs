using Cocoar.Shelf.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cocoar.Shelf.Tests;

public sealed class ManifestServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ManifestService _sut;

    public ManifestServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"shelf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var config = new TestReactiveConfig<ShelfOptions>(new ShelfOptions { DocsRoot = _tempDir });
        _sut = new ManifestService(config, NullLogger<ManifestService>.Instance);
    }

    [Fact]
    public void GetManifest_ReturnsNull_WhenProductDoesNotExist()
    {
        var result = _sut.GetManifest("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void GetManifest_ReturnsNull_WhenNoVersionDirectoriesExist()
    {
        var productDir = Path.Combine(_tempDir, "empty-product");
        Directory.CreateDirectory(productDir);

        var result = _sut.GetManifest("empty-product");

        Assert.Null(result);
    }

    [Fact]
    public void GetManifest_DetectsVersionsFromFilesystem()
    {
        var productDir = Path.Combine(_tempDir, "configuration");
        Directory.CreateDirectory(Path.Combine(productDir, "v4"));
        Directory.CreateDirectory(Path.Combine(productDir, "v5"));

        var result = _sut.GetManifest("configuration");

        Assert.NotNull(result);
        Assert.Equal("v5", result.Latest);
        Assert.Equal(["v5", "v4"], result.Versions);
    }

    [Fact]
    public void GetManifest_IgnoresNonVersionDirectories()
    {
        var productDir = Path.Combine(_tempDir, "withextras");
        Directory.CreateDirectory(Path.Combine(productDir, "v1"));
        Directory.CreateDirectory(Path.Combine(productDir, "v2"));
        Directory.CreateDirectory(Path.Combine(productDir, "assets"));
        Directory.CreateDirectory(Path.Combine(productDir, ".hidden"));

        var result = _sut.GetManifest("withextras");

        Assert.NotNull(result);
        Assert.Equal("v2", result.Latest);
        Assert.Equal(["v2", "v1"], result.Versions);
    }

    [Fact]
    public void GetManifest_LatestIsHighestVersion()
    {
        var productDir = Path.Combine(_tempDir, "ordering");
        Directory.CreateDirectory(Path.Combine(productDir, "v2"));
        Directory.CreateDirectory(Path.Combine(productDir, "v10"));
        Directory.CreateDirectory(Path.Combine(productDir, "v3"));

        var result = _sut.GetManifest("ordering");

        Assert.NotNull(result);
        Assert.Equal("v10", result.Latest);
    }

    [Fact]
    public void GetManifest_SupportsSemVerVersions()
    {
        var productDir = Path.Combine(_tempDir, "semver");
        Directory.CreateDirectory(Path.Combine(productDir, "5.1.0"));
        Directory.CreateDirectory(Path.Combine(productDir, "5.2.0"));
        Directory.CreateDirectory(Path.Combine(productDir, "5.0.0"));

        var result = _sut.GetManifest("semver");

        Assert.NotNull(result);
        Assert.Equal("5.2.0", result.Latest);
        Assert.Equal(["5.2.0", "5.1.0", "5.0.0"], result.Versions);
    }

    [Fact]
    public void GetManifest_SortsMajorMinorPatchCorrectly()
    {
        var productDir = Path.Combine(_tempDir, "sorting");
        Directory.CreateDirectory(Path.Combine(productDir, "v1.0.0"));
        Directory.CreateDirectory(Path.Combine(productDir, "v2.1.0"));
        Directory.CreateDirectory(Path.Combine(productDir, "v2.0.3"));
        Directory.CreateDirectory(Path.Combine(productDir, "v2.1.1"));

        var result = _sut.GetManifest("sorting");

        Assert.NotNull(result);
        Assert.Equal("v2.1.1", result.Latest);
        Assert.Equal(["v2.1.1", "v2.1.0", "v2.0.3", "v1.0.0"], result.Versions);
    }

    [Fact]
    public void GetManifest_LatestPrefersStableOverPreRelease()
    {
        var productDir = Path.Combine(_tempDir, "prerelease");
        Directory.CreateDirectory(Path.Combine(productDir, "v5.0.0"));
        Directory.CreateDirectory(Path.Combine(productDir, "v6.0.0-beta.1"));

        var result = _sut.GetManifest("prerelease");

        Assert.NotNull(result);
        Assert.Equal("v5.0.0", result.Latest);
        Assert.Equal(["v6.0.0-beta.1", "v5.0.0"], result.Versions);
    }

    [Fact]
    public void GetManifest_FallsBackToPreReleaseWhenNoStable()
    {
        var productDir = Path.Combine(_tempDir, "onlypre");
        Directory.CreateDirectory(Path.Combine(productDir, "v1.0.0-alpha.1"));
        Directory.CreateDirectory(Path.Combine(productDir, "v1.0.0-beta.1"));

        var result = _sut.GetManifest("onlypre");

        Assert.NotNull(result);
        Assert.Equal("v1.0.0-beta.1", result.Latest);
    }

    [Fact]
    public void GetManifest_SupportsMajorMinorWithoutPatch()
    {
        var productDir = Path.Combine(_tempDir, "majorminor");
        Directory.CreateDirectory(Path.Combine(productDir, "v5.1"));
        Directory.CreateDirectory(Path.Combine(productDir, "v5.2"));

        var result = _sut.GetManifest("majorminor");

        Assert.NotNull(result);
        Assert.Equal("v5.2", result.Latest);
        Assert.Equal(["v5.2", "v5.1"], result.Versions);
    }

    [Fact]
    public void GetManifest_ReturnsCachedResult_OnSecondCall()
    {
        var productDir = Path.Combine(_tempDir, "cached");
        Directory.CreateDirectory(Path.Combine(productDir, "v1"));

        var first = _sut.GetManifest("cached");
        var second = _sut.GetManifest("cached");

        Assert.Same(first, second);
    }

    [Fact]
    public void GetProducts_ReturnsProductDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "configuration"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "capabilities"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "filesystem"));

        var products = _sut.GetProducts();

        Assert.Equal(["capabilities", "configuration", "filesystem"], products);
    }

    [Fact]
    public void GetProducts_ReturnsEmpty_WhenNoProducts()
    {
        var config = new TestReactiveConfig<ShelfOptions>(new ShelfOptions { DocsRoot = Path.Combine(_tempDir, "nonexistent") });
        using var sut = new ManifestService(config, NullLogger<ManifestService>.Instance);

        var products = sut.GetProducts();

        Assert.Empty(products);
    }

    public void Dispose()
    {
        _sut.Dispose();

        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* cleanup best-effort */ }
    }
}
