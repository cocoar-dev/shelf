using Cocoar.Configuration.Reactive;
using MailKit.Net.Smtp;
using MimeKit;

namespace Cocoar.Shelf.Services.Email;

public sealed partial class SmtpEmailService : IEmailService
{
    private readonly IReactiveConfig<ShelfOptions> _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IReactiveConfig<ShelfOptions> config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var smtp = _config.CurrentValue.Email.Smtp;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName ?? "Shelf", smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseSsl, ct);

        if (!string.IsNullOrEmpty(smtp.UserName))
            await client.AuthenticateAsync(smtp.UserName, smtp.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        LogEmailSent(_logger, to, subject);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent via SMTP to {To}: {Subject}")]
    private static partial void LogEmailSent(ILogger logger, string to, string subject);
}
