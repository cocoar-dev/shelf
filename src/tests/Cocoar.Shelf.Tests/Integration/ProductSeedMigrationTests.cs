using Cocoar.Configuration.Reactive;
using Cocoar.Shelf;
using Cocoar.Shelf.Models;
using Cocoar.Shelf.Services;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cocoar.Shelf.Tests.Integration;

/// <summary>
/// The product JSON seed is one-time: once seeded, a product deleted via the UI/API must not be
/// resurrected from a lingering seed file on the next startup.
/// </summary>
[Collection("Integration")]
public class ProductSeedMigrationTests
{
    private readonly ShelfFixture _fixture;

    public ProductSeedMigrationTests(ShelfFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_DoesNotReimportSeedFiles_OnceSeeded()
    {
        var store = _fixture.Services.GetRequiredService<IDocumentStore>();
        var config = _fixture.Services.GetRequiredService<IReactiveConfig<ShelfOptions>>();

        // Mark the seed as already done (as it is after the first-ever startup).
        await using (var session = store.LightweightSession())
        {
            session.Store(new SeedState { Id = "products", SeededAt = DateTimeOffset.UtcNow });
            await session.SaveChangesAsync();
        }

        // Drop a seed JSON for a product that is NOT in the DB — the classic "deleted, but the file
        // is still on disk" situation.
        var name = "seed-ghost-" + Guid.NewGuid().ToString("N")[..8];
        var jsonPath = Path.Combine(_fixture.ConfigRoot, "products", name + ".json");
        await File.WriteAllTextAsync(jsonPath, $$"""{"Name":"{{name}}","DisplayName":"Ghost","Source":"upload"}""");

        try
        {
            // Run the migration again (simulates a restart).
            var migration = new ProductConfigMigrationService(store, config, NullLogger<ProductConfigMigrationService>.Instance);
            await migration.StartAsync(default);

            // The ghost must NOT be re-imported — the seed marker stops the JSON from being read.
            await using var query = store.QuerySession();
            Assert.Null(await query.LoadAsync<ProductConfig>(name));
        }
        finally
        {
            File.Delete(jsonPath);
        }
    }
}
