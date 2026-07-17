namespace Cocoar.Shelf.Services;

public sealed partial class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        LogEmail(_logger, to, subject, htmlBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email to {To}: {Subject}\n{Body}")]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}
