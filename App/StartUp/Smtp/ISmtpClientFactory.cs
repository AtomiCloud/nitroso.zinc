namespace App.StartUp.Smtp;

public interface ISmtpClientFactory
{
  ISmtpClient Get(string mailbox);
}
