using App.Error.V1;
using App.Modules.Users.Data;
using App.StartUp.Database;
using App.StartUp.Email;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.StartUp.Smtp;
using App.Utility;
using CSharp_Result;
using Domain;
using Domain.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace App.Modules.Announcements;

public class AnnouncementService(
  MainDbContext db,
  ISmtpClientFactory smtpClientFactory,
  IEmailRenderer emailRenderer,
  IOptionsMonitor<DomainOptions> options,
  IFeeCalculator feeCalculator,
  ILogger<AnnouncementService> logger
) : IAnnouncementService
{
  public async Task<Result<UserPrincipal>> SendWithdrawalFeeAnnouncement(string userId)
  {
    try
    {
      logger.LogInformation("Sending withdrawal fee announcement to user {UserId}", userId);
      var user = await db.Users.Where(x => x.Id == userId).FirstOrDefaultAsync();
      if (user == null)
        return new EntityNotFound("User Not Found", typeof(UserPrincipal), userId).ToException();
      if (string.IsNullOrWhiteSpace(user.Email))
        return new ValidationError(
          "User does not have an email address",
          new Dictionary<string, string[]>
          {
            ["Email"] = ["The user exists but has no email address to send to"],
          }
        ).ToException();

      var principal = user.ToPrincipal();
      return await this.Send(principal).Then(_ => principal, Errors.MapNone);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to send withdrawal fee announcement to user {UserId}", userId);
      return e;
    }
  }

  public async Task<Result<AnnouncementBroadcastResult>> BroadcastWithdrawalFeeAnnouncement()
  {
    try
    {
      var users = await db.Users.Where(x => x.Email != null && x.Email != "").ToArrayAsync();
      logger.LogInformation(
        "Broadcasting withdrawal fee announcement to {Count} users",
        users.Length
      );

      var sent = 0;
      var failed = new List<string>();
      foreach (var user in users)
      {
        var r = await this.Send(user.ToPrincipal());
        if (r.IsSuccess())
        {
          sent++;
        }
        else
        {
          logger.LogWarning(
            r.FailureOrDefault(),
            "Failed to send withdrawal fee announcement to user {UserId}",
            user.Id
          );
          failed.Add(user.Id);
        }
      }

      logger.LogInformation(
        "Withdrawal fee announcement broadcast complete: {Sent} sent, {Failed} failed",
        sent,
        failed.Count
      );
      return new AnnouncementBroadcastResult
      {
        Sent = sent,
        Failed = failed.Count,
        FailedUserIds = [.. failed],
      };
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to broadcast withdrawal fee announcement");
      return e;
    }
  }

  private async Task<Result<Unit>> Send(UserPrincipal user)
  {
    var o = options.CurrentValue;
    var feePercent = (feeCalculator.WithdrawFeeRate * 100).ToString("0.##");
    var smtpClient = smtpClientFactory.Get(SmtpProviders.Transactional);
    return await emailRenderer
      .RenderEmail(
        "withdrawal-fee-announcement",
        new
        {
          baseUrl = o.BaseUrl,
          whatsappUrl = o.WhatsAppUrl,
          telegramUrl = o.TelegramUrl,
          supportEmail = o.SupportEmail,
          userName = user.Record.Username.CapitalizeUsername(),
          userEmail = user.Record.Email,
          feePercent,
        }
      )
      .ThenAwait(
        async html =>
        {
          var smtpMessage = new SmtpEmailMessage
          {
            To = user.Record.Email!,
            Subject = $"BunnyBooker - Introducing a {feePercent}% Withdrawal Fee",
            Body = html,
            IsHtml = true,
          };
          // user id only: email addresses are PII and do not belong in logs
          logger.LogInformation(
            "Sending withdrawal fee announcement email to user {UserId}",
            user.Id
          );
          return await smtpClient.SendAsync(smtpMessage);
        },
        Errors.MapNone
      );
  }
}
