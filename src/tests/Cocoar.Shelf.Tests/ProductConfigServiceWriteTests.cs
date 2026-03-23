using System.Text.Json;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cocoar.Shelf.Tests;

public sealed class ProductConfigServiceWriteTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _productsDir;

    public ProductConfigServiceWriteTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"shelf-write-tests-{Guid.NewGuid():N}");
        _productsDir = Path.Combine(_tempDir, "products");
        Directory.CreateDirectory(_productsDir);
    }

    private ProductConfigService CreateService()
    {
        var config = new TestReactiveConfig<ShelfOptions>(new ShelfOptions { ConfigRoot = _tempDir });
        return new ProductConfigService(config, NullLogger<ProductConfigService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_WritesJsonFile()
    {
        using var sut = CreateService();
        var product = new ProductConfig { Name = "new-product", DisplayName = "New", Description = "Desc", Source = "upload" };

        await sut.CreateAsync(product);

        Assert.True(File.Exists(Path.Combine(_productsDir, "new-product.json")));
        var result = sut.GetConfig("new-product");
        Assert.NotNull(result);
        Assert.Equal("New", result.DisplayName);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenAlreadyExists()
    {
        File.WriteAllText(Path.Combine(_productsDir, "existing.json"),
            JsonSerializer.Serialize(new { name = "existing", source = "upload" }));
        using var sut = CreateService();

        var product = new ProductConfig { Name = "existing" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateAsync(product));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWithEmptyName()
    {
        using var sut = CreateService();
        var product = new ProductConfig { Name = "" };

        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(product));
    }

    [Fact]
    public async Task CreateAsync_IsImmediatelyReadable()
    {
        using var sut = CreateService();
        var product = new ProductConfig { Name = "immediate-read", DisplayName = "Test" };

        await sut.CreateAsync(product);

        // Should be in cache immediately, not waiting for FileSystemWatcher
        var result = sut.GetConfig("immediate-read");
        Assert.NotNull(result);
        Assert.Equal("Test", result.DisplayName);
    }

    [Fact]
    public async Task UpdateAsync_OverwritesExisting()
    {
        File.WriteAllText(Path.Combine(_productsDir, "to-update.json"),
            JsonSerializer.Serialize(new { name = "to-update", displayName = "Old", source = "upload" }));
        using var sut = CreateService();

        var updated = new ProductConfig { Name = "to-update", DisplayName = "New", Description = "Added", Source = "upload" };
        await sut.UpdateAsync(updated);

        var result = sut.GetConfig("to-update");
        Assert.NotNull(result);
        Assert.Equal("New", result.DisplayName);
        Assert.Equal("Added", result.Description);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsWhenNotFound()
    {
        using var sut = CreateService();
        var product = new ProductConfig { Name = "nonexistent" };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateAsync(product));
    }

    [Fact]
    public async Task DeleteAsync_RemovesFileAndReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_productsDir, "to-delete.json"),
            JsonSerializer.Serialize(new { name = "to-delete", source = "upload" }));
        using var sut = CreateService();

        Assert.NotNull(sut.GetConfig("to-delete"));

        var result = await sut.DeleteAsync("to-delete");

        Assert.True(result);
        Assert.False(File.Exists(Path.Combine(_productsDir, "to-delete.json")));
        Assert.Null(sut.GetConfig("to-delete"));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseWhenNotFound()
    {
        using var sut = CreateService();

        var result = await sut.DeleteAsync("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task CreateAsync_CreatesDirectoryIfMissing()
    {
        var emptyDir = Path.Combine(_tempDir, "empty-config");
        var config = new TestReactiveConfig<ShelfOptions>(new ShelfOptions { ConfigRoot = emptyDir });
        using var sut = new ProductConfigService(config, NullLogger<ProductConfigService>.Instance);

        var product = new ProductConfig { Name = "auto-dir" };
        await sut.CreateAsync(product);

        Assert.True(File.Exists(Path.Combine(emptyDir, "products", "auto-dir.json")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }
}
