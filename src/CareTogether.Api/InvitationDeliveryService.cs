using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;

namespace CareTogether.Api;

public sealed record InvitationDeliveryResult(bool Sent,string Message);

public sealed class InvitationDeliveryService(IConfiguration configuration,IHttpClientFactory clients,ILogger<InvitationDeliveryService> logger)
{
    public async Task<InvitationDeliveryResult> SendAsync(string channel,string destination,string subject,string body)
    {
        try
        {
            if(channel=="Text")return await SendText(destination,body);
            return await SendEmail(destination,subject,body);
        }
        catch(Exception ex) when(ex is SmtpException or HttpRequestException or FormatException)
        {
            logger.LogError(ex,"{Channel} invitation delivery failed for {Destination}",channel,destination);
            var provider=channel=="Text"?"text-message":"SMTP";
            return new(false,$"The invitation was saved, but {provider} delivery failed. Check the delivery-provider settings and try again.");
        }
    }
    async Task<InvitationDeliveryResult> SendEmail(string destination,string subject,string body)
    {
        var host=configuration["Smtp:Host"];if(string.IsNullOrWhiteSpace(host)){logger.LogWarning("SMTP is not configured. Invitation for {Destination}",destination);return new(false,"SMTP is not configured; the invitation link was saved for local testing.");}
        using var message=new MailMessage(configuration["Smtp:From"]??"noreply@caretogether.local",destination,subject,body);using var client=new SmtpClient(host,configuration.GetValue("Smtp:Port",587)){EnableSsl=configuration.GetValue("Smtp:UseSsl",true)};var user=configuration["Smtp:Username"];if(!string.IsNullOrWhiteSpace(user))client.Credentials=new NetworkCredential(user,configuration["Smtp:Password"]);await client.SendMailAsync(message);return new(true,"Invitation email sent.");
    }
    async Task<InvitationDeliveryResult> SendText(string destination,string body)
    {
        var sid=configuration["Twilio:AccountSid"];var token=configuration["Twilio:AuthToken"];var from=configuration["Twilio:FromPhone"];if(string.IsNullOrWhiteSpace(sid)||string.IsNullOrWhiteSpace(token)||string.IsNullOrWhiteSpace(from)){logger.LogWarning("Twilio is not configured. Text invitation for {Destination}",destination);return new(false,"Twilio is not configured; the invitation link was saved for local testing.");}
        using var request=new HttpRequestMessage(HttpMethod.Post,$"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json"){Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"To",destination},{"From",from},{"Body",body}})};request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{sid}:{token}")));var response=await clients.CreateClient().SendAsync(request);if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Twilio returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return new(true,"Invitation text message sent.");
    }
}
