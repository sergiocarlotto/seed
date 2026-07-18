using Microsoft.Extensions.Logging;

namespace Seed.Infrastructure.Email;

public class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        logger.LogInformation("Email (stub) para {To}: {Subject}", to, subject);
        return Task.CompletedTask;
    }
}
