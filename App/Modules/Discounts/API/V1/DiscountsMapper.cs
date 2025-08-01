using App.Modules.Users.API.V1;
using App.Modules.Wallets.API.V1;
using Domain.Discount;
using Domain.Withdrawal;

namespace App.Modules.Discounts.API.V1;

public static class DiscountMapper
{
  // Domain -> RES
  public static string ToRes(this DiscountType type) =>
    type switch
    {
      DiscountType.Flat => "Flat",
      DiscountType.Percentage => "Percentage",
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

  public static string ToRes(this DiscountMatchMode mode) =>
    mode switch
    {
      DiscountMatchMode.All => "All",
      DiscountMatchMode.Any => "Any",
      DiscountMatchMode.None => "None",
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

  public static string ToRes(this DiscountMatchType type) =>
    type switch
    {
      DiscountMatchType.UserId => "UserId",
      DiscountMatchType.Role => "Role",
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

  public static DiscountMatchRes ToRes(this DiscountMatch match) =>
    new(match.Value, match.Type.ToRes());

  public static DiscountTargetRes ToRes(this DiscountTarget target) =>
    new(target.MatchMode.ToRes(), target.Matches.Select(m => m.ToRes()).ToArray());

  public static DiscountRecordRes ToRes(this DiscountRecord record) =>
    new(record.Name, record.Description, record.Amount, record.Type.ToRes());

  public static DiscountStatusRes ToRes(this DiscountStatus status) => new(status.Disabled);

  public static DiscountPrincipalRes ToRes(this DiscountPrincipal d) =>
    new(d.Id, d.Record.ToRes(), d.Status.ToRes(), d.Target.ToRes());

  // REQ -> Domain
  public static DiscountType ToDiscountType(this string type) =>
    type switch
    {
      "Flat" => DiscountType.Flat,
      "Percentage" => DiscountType.Percentage,
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

  public static DiscountMatchMode ToDiscountMatchMode(this string mode) =>
    mode switch
    {
      "All" => DiscountMatchMode.All,
      "Any" => DiscountMatchMode.Any,
      "None" => DiscountMatchMode.None,
      _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

  public static DiscountMatchType ToDiscountMatchType(this string type) =>
    type switch
    {
      "UserId" => DiscountMatchType.UserId,
      "Role" => DiscountMatchType.Role,
      _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

  public static DiscountMatch ToDomain(this DiscountMatchReq match) =>
    new() { Value = match.Value, Type = match.MatchType.ToDiscountMatchType() };

  public static DiscountTarget ToDomain(this DiscountTargetReq target) =>
    new()
    {
      MatchMode = target.MatchMode.ToDiscountMatchMode(),
      Matches = target.Matches.Select(m => m.ToDomain()).ToArray(),
    };

  public static DiscountRecord ToDomain(this DiscountRecordReq record) =>
    new()
    {
      Name = record.Name,
      Description = record.Description,
      Amount = record.Amount,
      Type = record.Type.ToDiscountType(),
    };

  public static DiscountStatus ToDomain(this DiscountStatusReq status) =>
    new() { Disabled = status.Disabled };

  public static DiscountSearch ToDomain(this DiscountSearchQuery query) =>
    new()
    {
      Search = query.Search,
      Disabled = query.Disabled,
      DiscountType = query.DiscountType?.ToDiscountType(),
      MatchMode = query.MatchMode?.ToDiscountMatchMode(),
      MatchTarget = query.MatchTarget,
    };
}
