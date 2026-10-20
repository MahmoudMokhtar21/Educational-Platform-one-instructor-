using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Educatinal_Platform.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetAsync(string recipientEmail, string resetLink, CancellationToken cancellationToken = default);
    Task<bool> SendEmailVerificationAsync(string recipientEmail, string verificationLink, CancellationToken cancellationToken = default);
}

public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetAsync(string recipientEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        return await SendAsync(
            recipientEmail,
            "Reset your Educational Platform password",
            $"Use this link to reset your password: {resetLink}\n\nThis link expires in one hour.",
            "Password-reset",
            cancellationToken);
    }

    public async Task<bool> SendEmailVerificationAsync(string recipientEmail, string verificationLink, CancellationToken cancellationToken = default)
    {
        return await SendAsync(
            recipientEmail,
            "Verify your Educational Platform email",
            $"Verify your email address to activate your account: {verificationLink}\n\nThis link expires in 24 hours.",
            "Email-verification",
            cancellationToken);
    }

    private async Task<bool> SendAsync(string recipientEmail, string subject, string body, string operation, CancellationToken cancellationToken)
    {
        var host = _configuration["Email:Smtp:Host"]?.Trim();
        var username = _configuration["Email:Smtp:Username"]?.Trim();
        var password = _configuration["Email:Smtp:Password"]?.Trim();
        var from = _configuration["Email:From"]?.Trim();
        var port = _configuration.GetValue<int?>("Email:Smtp:Port") ?? 587;
        var enableSsl = _configuration.GetValue("Email:Smtp:EnableSsl", true);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogError("{Operation} email was not sent because SMTP settings are incomplete.", operation);
            return false;
        }

        // Google displays App Passwords in grouped characters. Whitespace is not part of
        // the credential and would cause SMTP AUTH to fail if it was copied into secrets.
        if (host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
            password = string.Concat(password.Where(character => !char.IsWhiteSpace(character)));

        _logger.LogInformation(
            "Sending {Operation} email through SMTP host {Host}:{Port}; SSL={EnableSsl}; username={Username}; from={From}; passwordConfigured={PasswordConfigured}; gmailFromMatchesUsername={GmailFromMatchesUsername}",
            operation,
            host,
            port,
            enableSsl,
            MaskEmail(username),
            MaskEmail(from),
            password.Length > 0,
            !host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase) || from.Equals(username, StringComparison.OrdinalIgnoreCase));

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            // Gmail on 587 requires an explicit STARTTLS upgrade before SMTP AUTH.
            // MailKit's StartTls option enforces that sequence rather than allowing
            // a clear-text authentication attempt.
            var socketOptions = enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "{Operation} email delivery failed. SMTP host {Host}:{Port}; SSL={EnableSsl}; username={Username}; from={From}; passwordConfigured={PasswordConfigured}; errorType={ErrorType}", operation, host, port, enableSsl, MaskEmail(username), MaskEmail(from), password.Length > 0, exception.GetType().Name);
            return false;
        }
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***";
        return $"{email[0]}***{email[atIndex - 1]}{email[atIndex..]}";
    }
}
