using CSharp_Result;
using Domain.Discount;
using Domain.Timings;
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
    var policies =
      spec == null
        ? Task.FromResult<Result<IEnumerable<CostPolicyPrincipal>>>(
          Array.Empty<CostPolicyPrincipal>()
        )
        : policyRepo.List();
    return this.Load(policies, userId, roles)
      .Then(
        tuple =>
        {
          var (p, d, c) = tuple;
          return this.Compute(userId, roles, spec, p, d, c.Record.Cost, DateTime.UtcNow);
        },
        Errors.MapNone
      );
  }

  // Batch per-slot pricing: base cost, policies and candidate discounts are
  // loaded ONCE, then each time gets the exact same per-spec math as
  // Materialize — one row per requested time, same order
  public Task<Result<IEnumerable<MaterializedCostSlot>>> MaterializeMany(
    string userId,
    string[] roles,
    DateOnly date,
    TrainDirection direction,
    IEnumerable<TimeOnly> times
  )
  {
    return this.Load(policyRepo.List(), userId, roles)
      .Then(
        tuple =>
        {
          var (p, d, c) = tuple;
          var now = DateTime.UtcNow;
          return times
            .Select(t => new MaterializedCostSlot
            {
              Time = t,
              Cost = this.Compute(
                userId,
                roles,
                new BookingCostSpec
                {
                  Date = date,
                  Time = t,
                  Direction = direction,
                },
                p,
                d,
                c.Record.Cost,
                now
              ),
            })
            .ToArray()
            .AsEnumerable();
        },
        Errors.MapNone
      );
  }

  // one load for everything pricing needs: policies (already resolved by the
  // caller), the caller's candidate discounts and the base cost row
  private Task<
    Result<(
      IEnumerable<CostPolicyPrincipal> Policies,
      IEnumerable<DiscountPrincipal> Discounts,
      CostPrincipal Cost
    )>
  > Load(Task<Result<IEnumerable<CostPolicyPrincipal>>> policies, string userId, string[] roles)
  {
    var r = roles.Concat([userId]).ToArray();
    return policies
      .ThenAwait(p =>
        discountRepo
          .Search(new DiscountSearch { MatchTarget = r, Disabled = false })
          .Then(d => (p, d), Errors.MapNone)
      )
      .ThenAwait(pd =>
        costRepo.GetCurrent().NullToError("latest").Then(c => (pd.p, pd.d, c), Errors.MapNone)
      );
  }

  // the single source of pricing math, shared by Materialize and
  // MaterializeMany so one slot of a batch always prices identically to the
  // single-spec endpoint
  private MaterializedCost Compute(
    string userId,
    string[] roles,
    BookingCostSpec? spec,
    IEnumerable<CostPolicyPrincipal> policies,
    IEnumerable<DiscountPrincipal> discounts,
    decimal baseCost,
    DateTime now
  )
  {
    var lines =
      spec == null
        ? []
        : policies
          .Where(x => CostPolicyMatcher.Applies(x.Record, spec, now))
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

    var discountsApplicable = discounts
      .Where(x => discountMatcher.Match(x.Target, x.Record, userId, roles, spec, now))
      .ToArray();

    logger.LogInformation(
      "Applying policies {@Policies} and discounts {@Discounts} to cost {@Cost}",
      lines,
      discountsApplicable,
      baseCost
    );

    // Monetary columns persist at numeric(16,8). Normalize here so the API
    // quote, stale-price validation and wallet transaction all use exactly
    // the same value PostgreSQL stores. PostgreSQL rounds numeric ties away
    // from zero; costs are non-negative but use the matching mode explicitly.
    var f = Math.Round(
      discountCalculator.Calculate(subtotal, discountsApplicable.Select(x => x.Record)),
      8,
      MidpointRounding.AwayFromZero
    );

    return new MaterializedCost
    {
      Cost = baseCost,
      PolicyLines = lines,
      Subtotal = subtotal,
      Final = f,
      Discounts = discountsApplicable.Select(x => x.Record),
    };
  }
}
