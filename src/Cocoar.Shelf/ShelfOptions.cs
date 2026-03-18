namespace Cocoar.Shelf;

public class ShelfOptions
{
    public string DocsRoot { get; set; } = "/data/docs";

    public string VersionPattern { get; set; } = @"^v\d+$";

    public string BasePlaceholder { get; set; } = "/__shelf__/";
}
