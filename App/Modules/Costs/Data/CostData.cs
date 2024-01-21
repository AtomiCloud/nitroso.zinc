using Microsoft.EntityFrameworkCore;

namespace App.Modules.Costs.Data;

public class CostData
{
  public Guid Id { get; set; }

  public DateTime CreatedAt { get; set; }

  // Record
  [Precision(16, 8)] public decimal Cost { get; set; }
}
