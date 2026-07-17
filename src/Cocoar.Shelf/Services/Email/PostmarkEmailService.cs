using Cocoar.Configuration.Reactive;
using PostmarkDotNet;

namespace Cocoar.Shelf.Services.Email;

public sealed partial class PostmarkEmailService : IEmailService
{
    private readonly IReactiveConfig<ShelfOptions> _config;
    private readonly ILogger<PostmarkEmailService> _logger;

    public PostmarkEmailService(IReactiveConfig<ShelfOptions> config, ILogger<PostmarkEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var pm = _config.CurrentValue.Email.Postmark;
        var client = new PostmarkClient(pm.ServerToken);

        var message = new PostmarkMessage
        {
            To = to,
            From = !string.IsNullOrEmpty(pm.FromName)
                ? $"{pm.FromName} <{pm.FromAddress}>"
                : pm.FromAddress,
            Subject = subject,
            HtmlBody = htmlBody,
            MessageStream = pm.MessageStream ?? "outbound"
        };

        var response = await client.SendMessageAsync(message);

        if (response.Status != PostmarkStatus.Success)
        {
            LogEmailFailed(_logger, to, subject, response.Message);
            throw new InvalidOperationException($"Postmark send failed: {response.Message}");
        }

        LogEmailSent(_logger, to, subject);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent via Postmark to {To}: {Subject}")]
    private static partial void LogEmailSent(ILogger logger, string to, string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "Postmark send failed to {To}: {Subject} — {Error}")]
    private static partial void LogEmailFailed(ILogger logger, string to, string subject, string error);
}
