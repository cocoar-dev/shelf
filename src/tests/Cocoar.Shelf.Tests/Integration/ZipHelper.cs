using System.IO.Compression;

namespace Cocoar.Shelf.Tests.Integration;

internal static class ZipHelper
{
    public static MemoryStream Create(params (string name, string content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }

    public static ByteArrayContent ToContent(MemoryStream zip)
    {
        var content = new ByteArrayContent(zip.ToArray());
        content.Headers.ContentType = new("application/zip");
        return content;
    }
}
