using CSharp_Result;

namespace App.StartUp.Smtp;

public interface ISmtpClient
{
  Task<Result<Unit>> SendAsync(SmtpEmailMessage email, CancellationToken cancellationToken = default);
  
  string Mailbox { get; }
}
