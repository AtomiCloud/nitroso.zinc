namespace App.StartUp.Smtp;

public interface ISmtpClientFactory
{
  /// <summary>
  /// Attempts to resolve a configured SMTP client for the given mailbox.
  /// Returns true when found; otherwise false.
  /// </summary>
  ISmtpClient Get(string mailbox);
}
