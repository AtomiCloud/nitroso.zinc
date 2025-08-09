using System.Net;
using System.Net.Mail;
using App.StartUp.Options;
using CSharp_Result;

namespace App.StartUp.Smtp;

public class NativeSmtpClient(
  SmtpOption config,
  string mailbox,
  ILogger<NativeSmtpClient> logger)
  : ISmtpClient
{
  public string Mailbox { get; } = mailbox;
  public async Task<Result<Unit>> SendAsync(SmtpEmailMessage email)
  {
    try
    {
      using var client = new SmtpClient(config.Host, config.Port);
      client.EnableSsl = config.EnableSsl;
      client.UseDefaultCredentials = config.UseDefaultCredentials;
      client.Credentials = new NetworkCredential(config.Username, config.Password);
      client.Timeout = config.Timeout;

      var fromEmail = !string.IsNullOrWhiteSpace(email.FromEmail) ? email.FromEmail : config.FromEmail;
      var fromName = !string.IsNullOrWhiteSpace(email.FromName) ? email.FromName : config.FromName;

      using var message = new MailMessage();
      message.From = new MailAddress(fromEmail, fromName);
      message.Subject = email.Subject;
      message.Body = email.Body;
      message.IsBodyHtml = email.IsHtml;

      message.To.Add(email.To);
      
      logger.LogInformation("Sending email via {Mailbox} to {To} with subject '{Subject}'", this.Mailbox, email.To, email.Subject);
      await client.SendMailAsync(message);
      logger.LogInformation("Email sent successfully via {Mailbox} to {To}", this.Mailbox, email.To);
      
      return new Unit();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to send email via {Mailbox} to {To} with subject '{Subject}'", this.Mailbox, email.To, email.Subject);
      throw;
    }
  }
}
