using App.Modules.Transactions.API.V1;
using App.Modules.Wallets.API.V1;
using App.Utility;
using Domain;
using Domain.Payment;

namespace App.Modules.Payments.API.V1;

public static class PaymentMapper
{
  // RES
  public static PaymentPrincipalRes ToRes(this PaymentPrincipal p) =>
    new(
      p.Reference.Id,
      p.Reference.ExternalReference,
      p.Reference.Gateway,
      p.CreatedAt,
      p.Statuses,
      p.Record.Amount,
      p.Record.CapturedAmount,
      p.Record.Currency,
      p.Record.Status,
      p.Record.LastUpdated,
      p.Record.AdditionalData
    );

  public static CreatePaymentRes ToRes(this PaymentPrincipal p, PaymentSecret secret) =>
    new(
      p.Reference.Id,
      p.Reference.ExternalReference,
      p.Reference.Gateway,
      secret.Secret,
      p.CreatedAt,
      p.Statuses,
      p.Record.Amount,
      p.Record.Currency,
      p.Record.Status,
      p.Record.LastUpdated,
      p.Record.AdditionalData
    );

  public static PaymentRes ToRes(this Payment p) =>
    new(p.Principal.ToRes(), p.Wallet.ToRes(), p.Transaction?.ToRes());

  public static CapturedPaymentRes ToRes(this CapturedPayment p) =>
    new(p.PaymentIntentId, p.CapturedAmount, p.Currency, p.CreatedAt, p.Status);

  public static GatewayFeeSyncRes ToRes(this GatewayFeeSyncResult r) =>
    new(r.Synced, r.Missing, r.HasMore);

  // REQ
  public static GatewayFeeSyncQuery ToDomain(this GatewayFeeSyncQueryReq q) =>
    new() { After = q.After?.ToDate(), Before = q.Before?.ToDate() };

  public static CapturedPaymentsQuery ToDomain(this CapturedPaymentsQueryReq q) =>
    new()
    {
      After = q.After?.ToDate(),
      Before = q.Before?.ToDate(),
      Limit = q.Limit ?? 100,
    };

  public static PaymentSearch ToDomain(this SearchPaymentQuery q)
  {
    var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
    return new PaymentSearch
    {
      Id = q.Id,
      WalletId = q.WalletId,
      TransactionId = q.TransactionId,
      Reference = q.Reference,
      Gateway = q.Gateway,
      MinAmount = q.Min,
      MaxAmount = q.Max,
      CreatedBefore = q.CreatedBefore?.ToDate().ToZonedDateTime(TimeOnly.MaxValue, tz),
      CreatedAfter = q.CreatedAfter?.ToDate().ToZonedDateTime(TimeOnly.MinValue, tz),
      LastUpdatedBefore = q.LastUpdatedBefore?.ToDate().ToZonedDateTime(TimeOnly.MaxValue, tz),
      LastUpdatedAfter = q.LastUpdatedAfter?.ToDate().ToZonedDateTime(TimeOnly.MinValue, tz),
      Status = q.Status,
      Limit = q.Limit ?? 20,
      Skip = q.Skip ?? 0,
    };
  }
}
