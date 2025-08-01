namespace Domain.Discount;

public interface IDiscountCalculator
{
  decimal Calculate(decimal cost, IEnumerable<DiscountRecord> record);
}

public class DiscountCalculator : IDiscountCalculator
{
  public decimal Calculate(decimal cost, IEnumerable<DiscountRecord> records)
  {
    var discount =
      records
        .Select(r =>
          r.Type switch
          {
            DiscountType.Percentage => r.Amount * cost,
            DiscountType.Flat => r.Amount,
            _ => throw new ArgumentOutOfRangeException(),
          }
        )
        ?.ToArray() ?? [];
    return Math.Max(discount.Aggregate(cost, (current, d) => current - d), 0);
  }
}
