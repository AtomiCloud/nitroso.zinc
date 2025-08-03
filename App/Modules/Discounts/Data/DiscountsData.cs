using Microsoft.EntityFrameworkCore;

namespace App.Modules.Discounts.Data;

public class DiscountData
{
  public Guid Id { get; set; }

  // Record
  [Precision(16, 8)]
  public decimal Amount { get; set; }

  public string DiscountType { get; set; } = string.Empty;

  public string Name { get; set; } = string.Empty;

  public string Description { get; set; } = string.Empty;

  // Target
  public DiscountTargetData Target { get; set; } = new();

  // Status
  public bool Disabled { get; set; }
}

public class DiscountTargetData
{
  public string MatchMode { get; set; } = string.Empty;

  public List<DiscountMatchData> Matches { get; set; } = [];
}

public class DiscountMatchData
{
  public string Value { get; set; } = string.Empty;

  public string Type { get; set; } = string.Empty;
}
