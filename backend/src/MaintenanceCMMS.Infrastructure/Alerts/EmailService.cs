using System.Net.Mail;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Alerts;

public sealed class EmailService : IEmailService
{
    private readonly MailOptions _options;

    public EmailService(IOptions<MailOptions> options)
    {
        _options = options.Value;
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        var provider = string.IsNullOrWhiteSpace(_options.Provider) ? "Development" : _options.Provider.Trim();
        if (provider.Equals("MicrosoftGraph", StringComparison.OrdinalIgnoreCase))
        {
            return new EmailSendResult(false, provider, null, "Microsoft Graph esta preparado como placeholder.");
        }

        if (provider.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            return new EmailSendResult(true, provider, DateTimeOffset.UtcNow);
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(ResolveSender()),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };

            foreach (var recipient in message.Recipients.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                mail.To.Add(recipient.Trim());
            }

            using var client = new SmtpClient(_options.Host, _options.Port);
            await client.SendMailAsync(mail, cancellationToken);

            return new EmailSendResult(true, provider, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new EmailSendResult(false, provider, null, ex.Message);
        }
    }

    private string ResolveSender()
    {
        if (!string.IsNullOrWhiteSpace(_options.PlanningEmail))
        {
            return _options.PlanningEmail;
        }

        return string.IsNullOrWhiteSpace(_options.From) ? "planificacion@example.local" : _options.From;
    }
}
