using CSharp_Result;
using Domain.Cost;
using Domain.Discount;
using Domain.Timings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Costs;

// MaterializeMany is the batch sibling of Materialize: it loads base cost,
// policies and candidate discounts once, then every requested time must
// price EXACTLY as Materialize would for the same spec — same policy lines,
// same discounts, same subtotal and final, in request order.
public class CostServiceMaterializeManyTests
{
  private static readonly DateOnly Date = new(2026, 7, 15); // a Wednesday
  private const TrainDirection Direction = TrainDirection.JToW;

  private static readonly TimeOnly[] Times =
  [
    new(5, 0),
    new(8, 30),
    new(12, 45),
    new(22, 45),
  ];

  private static CostService Make(
    decimal baseCost,
    IEnumerable<CostPolicyPrincipal>? policies = null,
    IEnumerable<DiscountPrincipal>? discounts = null
  ) =>
    new(
      new FakeCostRepository(baseCost),
      new FakeCostPolicyRepository(policies ?? []),
      new FakeDiscountRepository(discounts ?? []),
      new DiscountMatcher(NullLogger<DiscountMatcher>.Instance),
      new DiscountCalculator(),
      NullLogger<CostService>.Instance
    );

  private static CostPolicyPrincipal Policy(
    string name,
    decimal amount,
    bool isPercentage = false,
    bool enabled = true,
    TimeOnly? matchTime = null,
    TrainDirection? matchDirection = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      CreatedAt = DateTime.UtcNow,
      Record = new CostPolicyRecord
      {
        Name = name,
        Enabled = enabled,
        MatchDate = null,
        MatchTime = matchTime,
        MatchDayOfWeek = null,
        MatchDirection = matchDirection,
        LeadTimeUnderHours = null,
        Amount = amount,
        IsPercentage = isPercentage,
        EffectiveAt = null,
        ExpiresAt = null,
      },
    };

  private static DiscountPrincipal Discount(
    string name,
    decimal amount,
    TimeOnly? matchTime = null,
    string? role = null
  ) =>
    new()
    {
      Id = Guid.NewGuid(),
      Record = new DiscountRecord
      {
        Name = name,
        Description = "test discount",
        Amount = amount,
        Type = DiscountType.Flat,
        MatchTime = matchTime,
      },
      Target =
        role == null
          ? new DiscountTarget { MatchMode = DiscountMatchMode.None, Matches = [] }
          : new DiscountTarget
          {
            MatchMode = DiscountMatchMode.Any,
            Matches = [new DiscountMatch { Type = DiscountMatchType.Role, Value = role }],
          },
      Status = new DiscountStatus { Disabled = false },
    };

  [Fact]
  public async Task Every_slot_prices_exactly_like_materialize_would()
  {
    // a mix that exercises time-matched policies, direction-matched
    // policies, percentage lines, everyone-discounts and slot-targeted
    // discounts — the full per-spec pipeline
    var policies = new[]
    {
      Policy("peak 08:30 surcharge", 5m, matchTime: new TimeOnly(8, 30)),
      Policy("JToW promo", -10m, isPercentage: true, matchDirection: TrainDirection.JToW),
      Policy("disabled", 99m, enabled: false),
    };
    var discounts = new[]
    {
      Discount("everyone", 1m),
      Discount("early train", 2m, matchTime: new TimeOnly(5, 0)),
    };
    var service = Make(14m, policies, discounts);

    var batch = await service.MaterializeMany("user-1", ["vip"], Date, Direction, Times);
    batch.IsSuccess().Should().BeTrue();
    var slots = batch.SuccessOrDefault().ToArray();

    slots.Should().HaveCount(Times.Length);
    slots.Select(x => x.Time).Should().Equal(Times, "one row per time, in request order");

    foreach (var slot in slots)
    {
      var spec = new BookingCostSpec
      {
        Date = Date,
        Time = slot.Time,
        Direction = Direction,
      };
      var single = await service.Materialize("user-1", ["vip"], spec);
      single.IsSuccess().Should().BeTrue();
      var expected = single.SuccessOrDefault();

      slot.Cost.Cost.Should().Be(expected.Cost, $"base cost must match for {slot.Time}");
      slot.Cost.PolicyLines.Should()
        .BeEquivalentTo(
          expected.PolicyLines,
          o => o.WithStrictOrdering(),
          $"policy lines must match for {slot.Time}"
        );
      slot.Cost.Subtotal.Should().Be(expected.Subtotal, $"subtotal must match for {slot.Time}");
      slot.Cost.Discounts.Should()
        .BeEquivalentTo(
          expected.Discounts,
          o => o.WithStrictOrdering(),
          $"discounts must match for {slot.Time}"
        );
      slot.Cost.Final.Should().Be(expected.Final, $"final must match for {slot.Time}");
    }
  }

  [Fact]
  public async Task Slot_targeted_pricing_differs_between_slots_in_one_batch()
  {
    var service = Make(
      14m,
      [Policy("peak 08:30 surcharge", 5m, matchTime: new TimeOnly(8, 30))],
      [Discount("early train", 2m, matchTime: new TimeOnly(5, 0))]
    );

    var batch = await service.MaterializeMany(
      "user-1",
      [],
      Date,
      Direction,
      [new TimeOnly(5, 0), new TimeOnly(8, 30)]
    );
    var slots = batch.SuccessOrDefault().ToArray();

    var early = slots.Single(x => x.Time == new TimeOnly(5, 0));
    early.Cost.Subtotal.Should().Be(14m, "no surcharge at 05:00");
    early.Cost.Final.Should().Be(12m, "the early-train discount applies");

    var peak = slots.Single(x => x.Time == new TimeOnly(8, 30));
    peak.Cost.Subtotal.Should().Be(19m, "the 08:30 surcharge applies");
    peak.Cost.Final.Should().Be(19m, "the early-train discount does not");
  }

  [Fact]
  public async Task Batch_loads_policies_discounts_and_cost_exactly_once()
  {
    var costRepo = new FakeCostRepository(14m);
    var policyRepo = new FakeCostPolicyRepository([Policy("surcharge", 5m)]);
    var discountRepo = new FakeDiscountRepository([Discount("everyone", 1m)]);
    var service = new CostService(
      costRepo,
      policyRepo,
      discountRepo,
      new DiscountMatcher(NullLogger<DiscountMatcher>.Instance),
      new DiscountCalculator(),
      NullLogger<CostService>.Instance
    );

    var batch = await service.MaterializeMany("user-1", [], Date, Direction, Times);

    batch.IsSuccess().Should().BeTrue();
    costRepo.GetCurrentCalls.Should().Be(1, "the base cost is loaded once for the whole batch");
    policyRepo.ListCalls.Should().Be(1, "policies are loaded once for the whole batch");
    discountRepo.SearchCalls.Should().Be(1, "discounts are loaded once for the whole batch");
  }

  // ---- fakes ----

  private sealed class FakeCostRepository(decimal baseCost) : ICostRepository
  {
    public int GetCurrentCalls { get; private set; }

    public Task<Result<CostPrincipal?>> GetCurrent()
    {
      GetCurrentCalls++;
      return Task.FromResult(
        (Result<CostPrincipal?>)
          new CostPrincipal
          {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Record = new CostRecord { Cost = baseCost },
          }
      );
    }

    public Task<Result<IEnumerable<CostPrincipal>>> History() =>
      throw new NotImplementedException();

    public Task<Result<CostPrincipal>> Create(CostRecord record) =>
      throw new NotImplementedException();
  }

  private sealed class FakeCostPolicyRepository(IEnumerable<CostPolicyPrincipal> policies)
    : ICostPolicyRepository
  {
    public int ListCalls { get; private set; }

    public Task<Result<IEnumerable<CostPolicyPrincipal>>> List()
    {
      ListCalls++;
      return Task.FromResult((Result<IEnumerable<CostPolicyPrincipal>>)policies.ToArray());
    }

    public Task<Result<CostPolicyPrincipal>> Create(CostPolicyRecord record) =>
      throw new NotImplementedException();

    public Task<Result<CostPolicyPrincipal?>> Update(Guid id, CostPolicyRecord record) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }

  private sealed class FakeDiscountRepository(IEnumerable<DiscountPrincipal> discounts)
    : IDiscountRepository
  {
    public int SearchCalls { get; private set; }

    public Task<Result<IEnumerable<DiscountPrincipal>>> Search(DiscountSearch search)
    {
      SearchCalls++;
      return Task.FromResult((Result<IEnumerable<DiscountPrincipal>>)discounts.ToArray());
    }

    public Task<Result<DiscountPrincipal?>> Get(Guid id) => throw new NotImplementedException();

    public Task<Result<DiscountPrincipal>> Create(DiscountRecord record, DiscountTarget target) =>
      throw new NotImplementedException();

    public Task<Result<DiscountPrincipal?>> Update(
      Guid id,
      DiscountStatus? status,
      DiscountRecord? record,
      DiscountTarget? target
    ) => throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }
}
