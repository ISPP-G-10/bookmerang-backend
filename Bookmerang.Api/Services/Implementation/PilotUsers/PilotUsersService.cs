using System.Net;
using Bookmerang.Api.Services.Interfaces.PilotUsers;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Bookmerang.Api.Services.Implementation.PilotUsers
{
    public class PilotUsersService : IPilotUsersService
    {
        public async Task<bool> SendFeedbackMail(List<string> toEmails)
        {
            if (toEmails == null || toEmails.Count == 0)
            {
                return false;
            }

            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
            var templateId = Environment.GetEnvironmentVariable("SENDGRID_TEMPLATE_ID");
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("bookmerangproject@gmail.com", "Bookmerang Team");
            var appUrl = Environment.GetEnvironmentVariable("APP_URL");
            var feedbackUrl = Environment.GetEnvironmentVariable("FEEDBACK_URL");

            var recipients = toEmails
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(email => new EmailAddress(email))
                .ToList();

            if (recipients.Count == 0)
            {
                return false;
            }

            var dynamicTemplateData = new
            {
                app_url = appUrl,
                feedback_url = feedbackUrl
            };

            var msg = MailHelper.CreateSingleTemplateEmailToMultipleRecipients(from, recipients, templateId, dynamicTemplateData);

            msg.SetReplyTo(new EmailAddress("bookmerangproject@gmail.com", "Bookmerang Team"));

            var response = await client.SendEmailAsync(msg);

            if (response.StatusCode == HttpStatusCode.Accepted)
                return true;

            var body = await response.Body.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Error enviando correos con SendGrid. StatusCode: {(int)response.StatusCode}. Body: {body}"
            );
        }
    }
}