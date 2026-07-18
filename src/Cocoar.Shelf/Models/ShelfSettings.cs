namespace Cocoar.Shelf.Models;

/// <summary>
/// Runtime-editable global settings, stored as a single Marten document. Static deployment
/// config stays in <see cref="ShelfOptions"/> (file/env); anything an admin should manage
/// from the UI lives here.
/// </summary>
public class ShelfSettings
{
    public const string GlobalId = "global";

    public string Id { get; set; } = GlobalId;

    /// <summary>UI-managed master API key for CI/CD uploads. The config/env key
    /// (<see cref="ShelfOptions.ApiKey"/>) stays valid alongside as the bootstrap key.</summary>
    public string? MasterApiKey { get; set; }
}
