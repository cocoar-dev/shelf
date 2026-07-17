using System.Threading.Channels;
using Cocoar.Shelf.Models;

namespace Cocoar.Shelf.Services;

public sealed class AccessLogChannel
{
    private readonly Channel<AccessLogEntry> _channel = Channel.CreateUnbounded<AccessLogEntry>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<AccessLogEntry> Reader => _channel.Reader;

    public void Write(AccessLogEntry entry)
    {
        _channel.Writer.TryWrite(entry);
    }
}
