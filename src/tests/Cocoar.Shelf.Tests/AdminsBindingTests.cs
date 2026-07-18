using System.Text.Json;

namespace Cocoar.Shelf.Tests;

/// <summary>
/// Modgud.Admins must bind from both shapes: a JSON array (configuration.json) and a scalar
/// comma/semicolon-separated string (env vars — <c>Shelf__Modgud__Admins=a@x,b@y</c> — cannot
/// express JSON arrays; <c>__0</c>-indexed keys build a <c>{"0":…}</c> object that crashed the
/// container on docker-dev, 2026-07-18).
/// </summary>
public class AdminsBindingTests
{
    [Fact]
    public void Admins_BindsFromJsonArray()
    {
        var options = JsonSerializer.Deserialize<ShelfOptions>(
            """{"Modgud":{"Admins":["a@x.test"," b@y.test "]}}""")!;

        Assert.Equal(["a@x.test", "b@y.test"], options.Modgud.Admins);
    }

    [Theory]
    [InlineData("\"a@x.test,b@y.test\"")]
    [InlineData("\"a@x.test; b@y.test\"")]
    [InlineData("\" a@x.test ,, b@y.test \"")]
    public void Admins_BindsFromSeparatedString(string json)
    {
        var options = JsonSerializer.Deserialize<ShelfOptions>(
            "{\"Modgud\":{\"Admins\":" + json + "}}")!;

        Assert.Equal(["a@x.test", "b@y.test"], options.Modgud.Admins);
    }

    [Fact]
    public void Admins_EmptyString_BindsEmpty()
    {
        var options = JsonSerializer.Deserialize<ShelfOptions>(
            """{"Modgud":{"Admins":""}}""")!;

        Assert.Empty(options.Modgud.Admins);
    }
}
