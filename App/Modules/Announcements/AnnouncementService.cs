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
  IFeeRepository feeRepository,
  ILogger<AnnouncementService> logger
) : IAnnouncementService
{
  private const string DefaultReasoning =
    "Recently, we've seen widespread abuse of our wallet system, with large sums being deposited "
    + "and withdrawn purely to churn funds through the platform. This activity drives up costs "
    + "for everyone and puts the smooth, reliable service you count on at risk. To protect the "
    + "platform and the community of genuine travelers who use it, we're adjusting our fees.";

  // The fully-composed email content, resolved ONCE per announcement (not per
  // recipient) so a broadcast describes the same change to every user.
  private record AnnouncementContent
  {
    public required string FeeKind { get; init; }

    public required string Subject { get; init; }

    public required string Reasoning { get; init; }

    public required string ChangeLine { get; init; }

    public required string DeductLine { get; init; }

    public required string EffectiveLine { get; init; }
  }

  // What the announcement announces: the specific queued change when ChangeId
  // is given, else the NEXT scheduled change of the type, else the live fee
  // as an immediate change. Keeps the email in lockstep with what an admin
  // just scheduled in the fee editor.
  private async Task<Result<AnnouncementContent>> Resolve(FeeAnnouncementSpec spec)
  {
    decimal percentage,
      flatAmount;
    string? effectiveDate;

    var upcomingR = await feeRepository.GetUpcoming(spec.Type);
    if (upcomingR.IsFailure())
      return upcomingR.FailureOrDefault();
    var upcoming = upcomingR.SuccessOrDefault().ToArray();

    if (spec.ChangeId is { } changeId)
    {
      var change = upcoming.FirstOrDefault(x => x.Id == changeId);
      if (change != null)
      {
        (percentage, flatAmount) = (change.Percentage, change.FlatAmount);
        effectiveDate = FormatDate(change.EffectiveAt);
      }
      else
      {
        // an immediate change (or one that just took effect) is no longer in
        // the queue — announce it as the live fee instead of 404ing the
        // add-then-announce flow
        var liveR = await feeRepository.GetCurrent(spec.Type);
        if (liveR.IsFailure())
          return liveR.FailureOrDefault();
        var live = liveR.SuccessOrDefault();
        if (live == null || live.Id != changeId)
          return new EntityNotFound(
            "Queued Fee Change Not Found",
            typeof(FeeChange),
            changeId.ToString()
          ).ToException();
        (percentage, flatAmount) = (live.Percentage, live.FlatAmount);
        effectiveDate = null;
      }
    }
    else if (upcoming.FirstOrDefault() is { } next)
    {
      (percentage, flatAmount) = (next.Percentage, next.FlatAmount);
      effectiveDate = FormatDate(next.EffectiveAt);
    }
    else
    {
      var currentR = await feeCalculator.Current(spec.Type);
      if (currentR.IsFailure())
        return currentR.FailureOrDefault();
      var current = currentR.SuccessOrDefault();
      (percentage, flatAmount) = (current.Percentage, current.FlatAmount);
      effectiveDate = null;
    }

    var kind = spec.Type == FeeType.Withdrawal ? "withdrawal" : "deposit";
    var kindTitle = spec.Type == FeeType.Withdrawal ? "Withdrawal" : "Deposit";
    var removal = percentage == 0 && flatAmount == 0;
    var feeText = (Percentage: percentage, Flat: flatAmount) switch
    {
      { Percentage: > 0, Flat: > 0 } => $"{percentage:0.##}% + SGD {flatAmount:0.00}",
      { Percentage: > 0 } => $"{percentage:0.##}%",
      _ => $"SGD {flatAmount:0.00}",
    };

    return new AnnouncementContent
    {
      FeeKind = kind,
      Subject = removal
        ? $"BunnyBooker - Removing the {kindTitle} Fee"
          + (effectiveDate == null ? "" : $" on {effectiveDate}")
        : $"BunnyBooker - {kindTitle} Fee Update: {feeText}"
          + (effectiveDate == null ? "" : $" from {effectiveDate}"),
      Reasoning = string.IsNullOrWhiteSpace(spec.Reasoning)
        ? DefaultReasoning
        : spec.Reasoning.Trim(),
      ChangeLine = removal
        ? $"The {kind} fee is being removed — {kind}s will be free"
        : $"A {feeText} fee will apply to all wallet {kind}s",
      DeductLine = removal
        ? $"No fee will be charged on {kind}s"
        : spec.Type == FeeType.Withdrawal
          ? "The fee is deducted from the withdrawn amount"
          : "The fee is collected from the deposited amount",
      EffectiveLine =
        effectiveDate == null
          ? "This change is effective immediately"
          : $"This change takes effect on {effectiveDate}",
    };
  }

  private static string FormatDate(DateTime utc) => utc.ToString("d MMMM yyyy, HH:mm 'UTC'");

  public async Task<Result<UserPrincipal>> SendFeeAnnouncement(
    string userId,
    FeeAnnouncementSpec spec
  )
  {
    try
    {
      logger.LogInformation(
        "Sending {Type} fee announcement to user {UserId}",
        spec.Type,
        userId
      );
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

      var contentR = await this.Resolve(spec);
      if (contentR.IsFailure())
        return contentR.FailureOrDefault();

      var principal = user.ToPrincipal();
      return await this.Send(principal, contentR.SuccessOrDefault())
        .Then(_ => principal, Errors.MapNone);
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to send {Type} fee announcement to user {UserId}",
        spec.Type,
        userId
      );
      return e;
    }
  }

  public async Task<Result<AnnouncementBroadcastResult>> BroadcastFeeAnnouncement(
    FeeAnnouncementSpec spec
  )
  {
    try
    {
      var contentR = await this.Resolve(spec);
      if (contentR.IsFailure())
        return contentR.FailureOrDefault();
      var content = contentR.SuccessOrDefault();

      var users = await db.Users.Where(x => x.Email != null && x.Email != "").ToArrayAsync();
      logger.LogInformation(
        "Broadcasting {Type} fee announcement to {Count} users",
        spec.Type,
        users.Length
      );

      var sent = 0;
      var failed = new List<string>();
      foreach (var user in users)
      {
        var r = await this.Send(user.ToPrincipal(), content);
        if (r.IsSuccess())
        {
          sent++;
        }
        else
        {
          logger.LogWarning(
            r.FailureOrDefault(),
            "Failed to send {Type} fee announcement to user {UserId}",
            spec.Type,
            user.Id
          );
          failed.Add(user.Id);
        }
      }

      logger.LogInformation(
        "{Type} fee announcement broadcast complete: {Sent} sent, {Failed} failed",
        spec.Type,
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
      logger.LogError(e, "Failed to broadcast {Type} fee announcement", spec.Type);
      return e;
    }
  }

  private async Task<Result<Unit>> Send(UserPrincipal user, AnnouncementContent content)
  {
    var o = options.CurrentValue;
    var smtpClient = smtpClientFactory.Get(SmtpProviders.Transactional);
    return await emailRenderer
      .RenderEmail(
        "fee-announcement",
        new
        {
          baseUrl = o.BaseUrl,
          whatsappUrl = o.WhatsAppUrl,
          telegramUrl = o.TelegramUrl,
          supportEmail = o.SupportEmail,
          userName = user.Record.Username.CapitalizeUsername(),
          userEmail = user.Record.Email,
          feeKind = content.FeeKind,
          reasoning = content.Reasoning,
          changeLine = content.ChangeLine,
          deductLine = content.DeductLine,
          effectiveLine = content.EffectiveLine,
        }
      )
      .ThenAwait(
        async html =>
        {
          var smtpMessage = new SmtpEmailMessage
          {
            To = user.Record.Email!,
            Subject = content.Subject,
            Body = html,
            IsHtml = true,
          };
          // user id only: email addresses are PII and do not belong in logs
          logger.LogInformation("Sending fee announcement email to user {UserId}", user.Id);
          return await smtpClient.SendAsync(smtpMessage);
        },
        Errors.MapNone
      );
  }
}
