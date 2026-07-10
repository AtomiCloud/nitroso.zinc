using CSharp_Result;
using Domain.Cost;
using Domain.Discount;
using Domain.Timings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Costs;

// The pricing math must add up visibly: base (newest Cost row) + one delta
// per applicable policy = Subtotal (floored at 0), then discounts bring the
// Subtotal down to Final. Percentages use banker's rounding to even cents.
public class CostServiceMaterializeTests
{
  private static readonly BookingCostSpec Spec = new()
  {
    Date = new DateOnly(2026, 7, 15), // a Wednesday
    Time = new TimeOnly(8, 30),
    Direction = TrainDirection.JToW,
  };

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
        MatchTime = null,
        MatchDayOfWeek = null,
        MatchDirection = matchDirection,
        LeadTimeUnderHours = null,
        Amount = amount,
        IsPercentage = isPercentage,
        EffectiveAt = null,
        ExpiresAt = null,
      },
    };

  private static DiscountPrincipal FlatDiscountForEveryone(decimal amount) =>
    new()
    {
      Id = Guid.NewGuid(),
      Record = new DiscountRecord
      {
        Name = "flat",
        Description = "flat discount",
        Amount = amount,
        Type = DiscountType.Flat,
      },
      Target = new DiscountTarget { MatchMode = DiscountMatchMode.None, Matches = [] },
      Status = new DiscountStatus { Disabled = false },
    };

  private static DiscountPrincipal PercentageDiscountForEveryone(decimal amount) =>
    new()
    {
      Id = Guid.NewGuid(),
      Record = new DiscountRecord
      {
        Name = "percentage",
        Description = "percentage discount",
        Amount = amount,
        Type = DiscountType.Percentage,
      },
      Target = new DiscountTarget { MatchMode = DiscountMatchMode.None, Matches = [] },
      Status = new DiscountStatus { Disabled = false },
    };

  [Fact]
  public async Task Without_a_spec_no_policies_apply_and_subtotal_equals_base()
  {
    var service = Make(14m, [Policy("weekend surcharge", 5m)]);

    var result = await service.Materialize("user-1", [], null);

    result.IsSuccess().Should().BeTrue();
    var m = result.SuccessOrDefault();
    m.Cost.Should().Be(14m);
    m.PolicyLines.Should().BeEmpty();
    m.Subtotal.Should().Be(14m);
    m.Final.Should().Be(14m);
  }

  [Fact]
  public async Task Flat_and_percentage_policies_add_up_to_the_subtotal()
  {
    var service = Make(
      14m,
      [Policy("peak surcharge", 5m), Policy("promo", -10m, isPercentage: true)]
    );

    var result = await service.Materialize("user-1", [], Spec);

    result.IsSuccess().Should().BeTrue();
    var m = result.SuccessOrDefault();
    m.Cost.Should().Be(14m);
    m.PolicyLines.Should().HaveCount(2);
    m.PolicyLines.Single(x => x.Name == "peak surcharge").Delta.Should().Be(5m);
    m.PolicyLines.Single(x => x.Name == "promo").Delta.Should().Be(-1.40m, "-10% of 14");
    m.Subtotal.Should().Be(14m + 5m - 1.40m);
    m.Subtotal.Should().Be(m.Cost + m.PolicyLines.Sum(x => x.Delta), "the math must add up");
    m.Final.Should().Be(m.Subtotal, "no discounts configured");
  }

  [Fact]
  public async Task Percentage_deltas_use_bankers_rounding_to_even_cents()
  {
    // 0.5% of 25 = 0.125 → rounds to 0.12 (even), not 0.13
    var even = await Make(25m, [Policy("p", 0.5m, isPercentage: true)])
      .Materialize("user-1", [], Spec);
    even.SuccessOrDefault().PolicyLines.Single().Delta.Should().Be(0.12m);

    // 1.5% of 25 = 0.375 → rounds to 0.38 (even)
    var odd = await Make(25m, [Policy("p", 1.5m, isPercentage: true)])
      .Materialize("user-1", [], Spec);
    odd.SuccessOrDefault().PolicyLines.Single().Delta.Should().Be(0.38m);
  }

  [Fact]
  public async Task Subtotal_is_floored_at_zero()
  {
    var service = Make(14m, [Policy("mega discount", -20m)]);

    var result = await service.Materialize("user-1", [], Spec);

    var m = result.SuccessOrDefault();
    m.Subtotal.Should().Be(0m, "base 14 - 20 floors at 0");
    m.Final.Should().Be(0m);
  }

  [Fact]
  public async Task Disabled_and_non_matching_policies_contribute_no_lines()
  {
    var service = Make(
      14m,
      [
        Policy("disabled", 5m, enabled: false),
        Policy("wrong direction", 5m, matchDirection: TrainDirection.WToJ),
        Policy("applies", 2m, matchDirection: TrainDirection.JToW),
      ]
    );

    var result = await service.Materialize("user-1", [], Spec);

    var m = result.SuccessOrDefault();
    m.PolicyLines.Should().ContainSingle(x => x.Name == "applies");
    m.Subtotal.Should().Be(16m);
  }

  [Fact]
  public async Task Discounts_apply_on_the_policy_subtotal_and_everything_adds_up()
  {
    var service = Make(
      14m,
      [Policy("peak surcharge", 6m)],
      [FlatDiscountForEveryone(3m)]
    );

    var result = await service.Materialize("user-1", [], Spec);

    var m = result.SuccessOrDefault();
    m.Cost.Should().Be(14m);
    m.Subtotal.Should().Be(20m, "base 14 + surcharge 6");
    m.Discounts.Should().ContainSingle();
    m.Final.Should().Be(17m, "the flat 3 discount applies on the subtotal, not the base");
    (m.Cost + m.PolicyLines.Sum(x => x.Delta) - m.Discounts.Sum(d => d.Amount))
      .Should()
      .Be(m.Final, "base + policies - discounts = final");
  }

  [Fact]
  public async Task Final_is_normalized_to_the_eight_decimals_persisted_by_wallets()
  {
    var service = Make(15.12345678m, discounts: [PercentageDiscountForEveryone(0.12345678m)]);

    var result = await service.Materialize("user-1", [], Spec);

    result.SuccessOrDefault().Final.Should().Be(13.25636350m);
  }

  // ---- fakes ----

  private sealed class FakeCostRepository(decimal baseCost) : ICostRepository
  {
    public Task<Result<CostPrincipal?>> GetCurrent() =>
      Task.FromResult(
        (Result<CostPrincipal?>)
          new CostPrincipal
          {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Record = new CostRecord { Cost = baseCost },
          }
      );

    public Task<Result<IEnumerable<CostPrincipal>>> History() =>
      throw new NotImplementedException();

    public Task<Result<CostPrincipal>> Create(CostRecord record) =>
      throw new NotImplementedException();
  }

  private sealed class FakeCostPolicyRepository(IEnumerable<CostPolicyPrincipal> policies)
    : ICostPolicyRepository
  {
    public Task<Result<IEnumerable<CostPolicyPrincipal>>> List() =>
      Task.FromResult((Result<IEnumerable<CostPolicyPrincipal>>)policies.ToArray());

    public Task<Result<CostPolicyPrincipal>> Create(CostPolicyRecord record) =>
      throw new NotImplementedException();

    public Task<Result<CostPolicyPrincipal?>> Update(Guid id, CostPolicyRecord record) =>
      throw new NotImplementedException();

    public Task<Result<Unit?>> Delete(Guid id) => throw new NotImplementedException();
  }

  private sealed class FakeDiscountRepository(IEnumerable<DiscountPrincipal> discounts)
    : IDiscountRepository
  {
    public Task<Result<IEnumerable<DiscountPrincipal>>> Search(DiscountSearch search) =>
      Task.FromResult((Result<IEnumerable<DiscountPrincipal>>)discounts.ToArray());

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
