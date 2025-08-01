using Domain.Cost;
using Domain.Discount;

namespace App.Modules.Discounts.Data;

public static class DiscountMapper
{
  // Data -> Domain
  public static DiscountMatchMode ToDiscountMatchMode(this string data) =>
    data switch
    {
      "any" => DiscountMatchMode.Any,
      "all" => DiscountMatchMode.All,
      "none" => DiscountMatchMode.None,
      _ => throw new ArgumentOutOfRangeException($"Invalid DiscountMatchMode: {data}"),
    };

  public static DiscountMatchType ToDiscountMatchType(this string data) =>
    data switch
    {
      "user_id" => DiscountMatchType.UserId,
      "role" => DiscountMatchType.Role,
      _ => throw new ArgumentOutOfRangeException($"Invalid DiscountMatchType: {data}"),
    };

  public static DiscountType ToDiscountType(this string data) =>
    data switch
    {
      "percentage" => DiscountType.Percentage,
      "flat" => DiscountType.Flat,
      _ => throw new ArgumentOutOfRangeException($"Invalid DiscountType: {data}"),
    };

  public static DiscountStatus ToStatus(this DiscountData data) =>
    new() { Disabled = data.Disabled };

  public static DiscountTarget ToTarget(this DiscountData data) =>
    new()
    {
      MatchMode = data.Target.MatchMode.ToDiscountMatchMode(),
      Matches = data
        .Target.Matches.Select(x => new DiscountMatch
        {
          Type = x.Type.ToDiscountMatchType(),
          Value = x.Value,
        })
        .ToList(),
    };

  public static DiscountRecord ToRecord(this DiscountData data) =>
    new()
    {
      Amount = data.Amount,
      Description = data.Description,
      Type = data.DiscountType.ToDiscountType(),
      Name = data.Name,
    };

  public static DiscountPrincipal ToPrincipal(this DiscountData data) =>
    new()
    {
      Id = data.Id,
      Record = data.ToRecord(),
      Status = data.ToStatus(),
      Target = data.ToTarget(),
    };

  // Domain -> Data
  public static string ToData(this DiscountType domain) =>
    domain switch
    {
      DiscountType.Flat => "flat",
      DiscountType.Percentage => "percentage",
      _ => throw new ArgumentOutOfRangeException($"Invalid DiscountType: {domain}"),
    };

  public static string ToData(this DiscountMatchMode domain) =>
    domain switch
    {
      DiscountMatchMode.All => "all",
      DiscountMatchMode.Any => "any",
      DiscountMatchMode.None => "none",
      _ => throw new ArgumentOutOfRangeException($"Invalid DiscountMatchMode: {domain}"),
    };

  public static string ToData(this DiscountMatchType domain) =>
    domain switch
    {
      DiscountMatchType.Role => "role",
      DiscountMatchType.UserId => "user_id",
      _ => throw new ArgumentOutOfRangeException($"Invalid DiscountMatchType: {domain}"),
    };

  public static DiscountData UpdateData(this DiscountData data, DiscountRecord record)
  {
    data.Amount = record.Amount;
    data.Description = record.Description;
    data.DiscountType = record.Type.ToData();
    data.Name = record.Name;
    return data;
  }

  public static DiscountData UpdateData(this DiscountData data, DiscountTarget target)
  {
    data.Target.MatchMode = target.MatchMode.ToData();
    data.Target.Matches = target
      .Matches.Select(x => new DiscountMatchData { Type = x.Type.ToData(), Value = x.Value })
      .ToList();
    return data;
  }

  public static DiscountData UpdateData(this DiscountData data, DiscountStatus status)
  {
    data.Disabled = status.Disabled;
    return data;
  }
}
