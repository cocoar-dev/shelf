namespace Cocoar.Shelf;

public class ShelfOptions
{
    public string DocsRoot { get; set; } = "/data/docs";

    public string ConfigRoot { get; set; } = "/data/config";

    public string PathBase { get; set; } = "";

    public string VersionPattern { get; set; } = @"^v?\d+(\.\d+(\.\d+(-[\w.]+)?)?)?$";

    public string BasePlaceholder { get; set; } = "/__shelf__/";

    public bool EnableLandingPage { get; set; }

    public string ApiKey { get; set; } = "";

    public long MaxUploadSizeBytes { get; set; } = 104_857_600; // 100 MB
}
