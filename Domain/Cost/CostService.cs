using CSharp_Result;
using Domain.Discount;
using Microsoft.Extensions.Logging;

namespace Domain.Cost;

public class CostService(
  ICostRepository costRepo,
  IDiscountRepository discountRepo,
  IDiscountMatcher discountMatcher,
  IDiscountCalculator discountCalculator,
  ILogger<CostService> logger
) : ICostService
{
  public Task<Result<IEnumerable<CostPrincipal>>> History()
  {
    return costRepo.History();
  }

  public Task<Result<CostPrincipal>> Create(CostRecord record)
  {
    return costRepo.Create(record);
  }

  public Task<Result<CostPrincipal?>> GetCurrent()
  {
    return costRepo.GetCurrent();
  }

  public Task<Result<MaterializedCost>> Materialize(string userId, string[] roles)
  {
    var r = roles.Concat([userId]).ToArray();
    return discountRepo
      .Search(new DiscountSearch { MatchTarget = r, Disabled = false })
      .ThenAwait(d => costRepo.GetCurrent().NullToError("latest").Then(c => (c, d), Errors.MapNone))
      .Then(
        tuple =>
        {
          var (c, d) = tuple;
          var discountsApplicable = d.Where(x => discountMatcher.Match(x.Target, userId, roles))
            .ToArray();

          logger.LogInformation(
            "Applying discounts {@Discounts} to cost {@Cost}",
            discountsApplicable,
            c.Record
          );

          var cost = c.Record.Cost;
          var f = discountCalculator.Calculate(cost, discountsApplicable.Select(x => x.Record));

          return new MaterializedCost
          {
            Cost = cost,
            Final = f,
            Discounts = discountsApplicable.Select(x => x.Record),
          };
        },
        Errors.MapNone
      );
  }
}
