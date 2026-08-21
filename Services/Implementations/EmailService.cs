using MailKit.Net.Smtp;
using MimeKit;
using UniLMS.API.Services.Interfaces;

namespace UniLMS.API.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendOtpAsync(string email, string otpCode)
    {
        var subject = "UniLMS - Password Reset OTP";
        var body = $@"
            <h2>Password Reset Request</h2>
            <p>Your OTP code is: <strong>{otpCode}</strong></p>
            <p>This code will expire in 10 minutes.</p>
            <p>If you didn't request this, please ignore this email.</p>";

        await SendEmailAsync(email, subject, body);
    }

    public async Task SendNotificationAsync(string email, string subject, string body)
    {
        await SendEmailAsync(email, subject, body);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _config["Email:SenderName"], _config["Email:SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Email:SmtpHost"],
                int.Parse(_config["Email:SmtpPort"]!),
                MailKit.Security.SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(
                _config["Email:SmtpUser"],
                _config["Email:SmtpPass"]);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            throw;
        }
    }
}
