using CSharp_Result;

namespace App.StartUp.Smtp;

public interface ISmtpClient
{
  Task<Result<Unit>> SendAsync(SmtpEmailMessage email);
  
  string Mailbox { get; }
}
