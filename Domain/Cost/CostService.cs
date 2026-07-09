using CSharp_Result;
using Domain.Discount;
using Microsoft.Extensions.Logging;

namespace Domain.Cost;

public class CostService(
  ICostRepository costRepo,
  ICostPolicyRepository policyRepo,
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

  public Task<Result<IEnumerable<CostPolicyPrincipal>>> ListPolicies()
  {
    return policyRepo.List();
  }

  public Task<Result<CostPolicyPrincipal>> CreatePolicy(CostPolicyRecord record)
  {
    return policyRepo.Create(record);
  }

  public Task<Result<CostPolicyPrincipal?>> UpdatePolicy(Guid id, CostPolicyRecord record)
  {
    return policyRepo.Update(id, record);
  }

  public Task<Result<Unit?>> DeletePolicy(Guid id)
  {
    return policyRepo.Delete(id);
  }

  // The breakdown always adds up: base (newest Cost row) + one line per
  // applicable policy = Subtotal (floored at 0), then per-user Discounts
  // bring the Subtotal down to Final. spec = null skips policies entirely.
  public Task<Result<MaterializedCost>> Materialize(
    string userId,
    string[] roles,
    BookingCostSpec? spec
  )
  {
    var r = roles.Concat([userId]).ToArray();
    var policies =
      spec == null
        ? Task.FromResult<Result<IEnumerable<CostPolicyPrincipal>>>(
          Array.Empty<CostPolicyPrincipal>()
        )
        : policyRepo.List();
    return policies
      .ThenAwait(p =>
        discountRepo
          .Search(new DiscountSearch { MatchTarget = r, Disabled = false })
          .Then(d => (p, d), Errors.MapNone)
      )
      .ThenAwait(pd =>
        costRepo.GetCurrent().NullToError("latest").Then(c => (pd.p, pd.d, c), Errors.MapNone)
      )
      .Then(
        tuple =>
        {
          var (p, d, c) = tuple;
          var baseCost = c.Record.Cost;
          var now = DateTime.UtcNow;

          var lines =
            spec == null
              ? []
              : p.Where(x => CostPolicyMatcher.Applies(x.Record, spec, now))
                .Select(x => new CostPolicyLine
                {
                  Name = x.Record.Name,
                  // banker's rounding keeps percentage lines in even cents
                  Delta = x.Record.IsPercentage
                    ? Math.Round(baseCost * x.Record.Amount / 100m, 2, MidpointRounding.ToEven)
                    : x.Record.Amount,
                })
                .ToArray();

          var subtotal = Math.Max(baseCost + lines.Sum(x => x.Delta), 0m);

          var discountsApplicable = d.Where(x => discountMatcher.Match(x.Target, userId, roles))
            .ToArray();

          logger.LogInformation(
            "Applying policies {@Policies} and discounts {@Discounts} to cost {@Cost}",
            lines,
            discountsApplicable,
            c.Record
          );

          var f = discountCalculator.Calculate(
            subtotal,
            discountsApplicable.Select(x => x.Record)
          );

          return new MaterializedCost
          {
            Cost = baseCost,
            PolicyLines = lines,
            Subtotal = subtotal,
            Final = f,
            Discounts = discountsApplicable.Select(x => x.Record),
          };
        },
        Errors.MapNone
      );
  }
}
