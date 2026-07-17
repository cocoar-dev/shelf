namespace Cocoar.Shelf.Services.Email;

public static class EmailTemplateStore
{
    public static (string Subject, string HtmlBody) Render(EmailTemplate template, Dictionary<string, string> model)
    {
        var (subject, body) = template switch
        {
            EmailTemplate.EmailOtp => ("Shelf Login Code", OtpTemplate),
            EmailTemplate.MagicLink => ("Shelf Login Link", MagicLinkTemplate),
            EmailTemplate.PasswordReset => ("Shelf Password Reset", PasswordResetTemplate),
            _ => throw new ArgumentOutOfRangeException(nameof(template))
        };

        foreach (var (key, value) in model)
        {
            subject = subject.Replace($"{{{{{key}}}}}", value);
            body = body.Replace($"{{{{{key}}}}}", value);
        }

        return (subject, body);
    }

    private const string OtpTemplate = """
        <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
            <h2 style="color: #2563eb;">Shelf Login Code</h2>
            <p>Your login code is:</p>
            <div style="font-size: 32px; font-weight: bold; letter-spacing: 8px; padding: 16px; background: #f3f4f6; border-radius: 8px; text-align: center; margin: 24px 0;">
                {{Code}}
            </div>
            <p style="color: #6b7280; font-size: 14px;">This code expires in {{ExpirationMinutes}} minutes.</p>
        </div>
        """;

    private const string MagicLinkTemplate = """
        <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
            <h2 style="color: #2563eb;">Shelf Login</h2>
            <p>Click the button below to sign in:</p>
            <div style="margin: 24px 0;">
                <a href="{{Link}}" style="display: inline-block; padding: 12px 32px; background: #2563eb; color: white; text-decoration: none; border-radius: 6px; font-weight: 600;">
                    Sign in to Shelf
                </a>
            </div>
            <p style="color: #6b7280; font-size: 14px;">This link expires in {{ExpirationMinutes}} minutes. If you didn't request this, ignore this email.</p>
        </div>
        """;

    private const string PasswordResetTemplate = """
        <div style="font-family: sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
            <h2 style="color: #2563eb;">Password Reset</h2>
            <p>Click the button below to reset your password:</p>
            <div style="margin: 24px 0;">
                <a href="{{Link}}" style="display: inline-block; padding: 12px 32px; background: #2563eb; color: white; text-decoration: none; border-radius: 6px; font-weight: 600;">
                    Reset Password
                </a>
            </div>
            <p style="color: #6b7280; font-size: 14px;">This link expires in 24 hours. If you didn't request this, ignore this email.</p>
        </div>
        """;
}
