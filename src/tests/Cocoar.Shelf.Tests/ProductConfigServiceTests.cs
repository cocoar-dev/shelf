using System.Text.Json;
using Cocoar.Shelf.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cocoar.Shelf.Tests;

public sealed class ProductConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _productsDir;

    public ProductConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"shelf-config-tests-{Guid.NewGuid():N}");
        _productsDir = Path.Combine(_tempDir, "products");
        Directory.CreateDirectory(_productsDir);
    }

    private ProductConfigService CreateService(string? configRoot = null)
    {
        var config = new TestReactiveConfig<ShelfOptions>(new ShelfOptions { ConfigRoot = configRoot ?? _tempDir });
        return new ProductConfigService(config, NullLogger<ProductConfigService>.Instance);
    }

    [Fact]
    public void GetConfig_ReturnsNull_WhenProductNotRegistered()
    {
        using var sut = CreateService();

        Assert.Null(sut.GetConfig("nonexistent"));
    }

    [Fact]
    public void GetConfig_ReturnsConfig_WhenJsonFileExists()
    {
        WriteConfig("myproduct", new { name = "myproduct", displayName = "My Product", description = "A test product", source = "upload" });
        using var sut = CreateService();

        var result = sut.GetConfig("myproduct");

        Assert.NotNull(result);
        Assert.Equal("myproduct", result.Name);
        Assert.Equal("My Product", result.DisplayName);
        Assert.Equal("A test product", result.Description);
        Assert.Equal("upload", result.Source);
    }

    [Fact]
    public void GetAll_ReturnsAllConfigs_Sorted()
    {
        WriteConfig("beta", new { name = "beta", source = "upload" });
        WriteConfig("alpha", new { name = "alpha", source = "upload" });
        using var sut = CreateService();

        var all = sut.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal("alpha", all[0].Name);
        Assert.Equal("beta", all[1].Name);
    }

    [Fact]
    public void GetConfig_ReturnsNull_WhenJsonIsInvalid()
    {
        File.WriteAllText(Path.Combine(_productsDir, "broken.json"), "not json{{{");
        using var sut = CreateService();

        Assert.Null(sut.GetConfig("broken"));
    }

    [Fact]
    public void Constructor_HandlesMissingConfigDirectory()
    {
        using var sut = CreateService(Path.Combine(_tempDir, "nonexistent"));

        Assert.Empty(sut.GetAll());
    }

    [Fact]
    public void GetConfig_UsesNameFromJsonNotFilename()
    {
        // File named "foo.json" but JSON name is "bar"
        WriteConfig("foo", new { name = "bar", source = "upload" });
        using var sut = CreateService();

        Assert.Null(sut.GetConfig("foo"));
        Assert.NotNull(sut.GetConfig("bar"));
    }

    [Fact]
    public void GetConfig_DefaultsSourceToUpload()
    {
        WriteConfig("minimal", new { name = "minimal" });
        using var sut = CreateService();

        var result = sut.GetConfig("minimal");

        Assert.NotNull(result);
        Assert.Equal("upload", result.Source);
    }

    private void WriteConfig(string filename, object config)
    {
        File.WriteAllText(
            Path.Combine(_productsDir, $"{filename}.json"),
            JsonSerializer.Serialize(config));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* cleanup best-effort */ }
    }
}
