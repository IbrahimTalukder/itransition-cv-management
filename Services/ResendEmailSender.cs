using System.Text;
using System.Text.Json;

namespace CvManagementSystem.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}

public class ResendEmailSender : IEmailSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<ResendEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var apiKey = _config["Resend:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "CHANGE_ME")
        {
         
            _logger.LogInformation("[EmailSender] Resend not configured. Would send to {Email}: {Subject}\n{Body}",
                toEmail, subject, htmlBody);
            return;
        }

       
        var fromAddress = _config["Resend:FromAddress"] ?? "onboarding@resend.dev";

        try
        {
            var payload = new
            {
                from = $"CV Management <{fromAddress}>",
                to = new[] { toEmail },
                subject,
                html = htmlBody
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email to {Email}. Status: {Status}. Body: {Body}",
                    toEmail, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {

            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}
