namespace App.StartUp.Smtp;

public record SmtpEmailMessage
{
  public required string To { get; init; }
  public required string Subject { get; init; }
  public required string Body { get; init; }
  public string? FromEmail { get; init; } = null;
  public string? FromName { get; init; } = null;
  public bool IsHtml { get; init; } = true;
}
