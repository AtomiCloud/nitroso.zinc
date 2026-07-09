using CSharp_Result;
using Domain.Cost;
using Domain.Discount;
using Domain.Timings;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Costs;

// Pricing roles = JWT roles ∪ admin-granted ExtraRoles (unioned in the Cost
// endpoints before calling Materialize). A role-targeted discount must apply
// whether the role rode in on the token or was granted via ExtraRoles — and
// roles from neither source never unlock it.
public class ExtraRolesPricingTests
{
  private static readonly BookingCostSpec Spec = new()
  {
    Date = new DateOnly(2026, 7, 15),
    Time = new TimeOnly(8, 30),
    Direction = TrainDirection.JToW,
  };

  private static CostService Make(IEnumerable<DiscountPrincipal> discounts) =>
    new(
      new FakeCostRepository(20m),
      new FakeCostPolicyRepository(),
      new FakeDiscountRepository(discounts),
      new DiscountMatcher(NullLogger<DiscountMatcher>.Instance),
      new DiscountCalculator(),
      NullLogger<CostService>.Instance
    );

  private static DiscountPrincipal RoleDiscount(string role, decimal amount) =>
    new()
    {
      Id = Guid.NewGuid(),
      Record = new DiscountRecord
      {
        Name = $"{role} discount",
        Description = "role targeted",
        Amount = amount,
        Type = DiscountType.Flat,
      },
      Target = new DiscountTarget
      {
        MatchMode = DiscountMatchMode.Any,
        Matches = [new DiscountMatch { Type = DiscountMatchType.Role, Value = role }],
      },
      Status = new DiscountStatus { Disabled = false },
    };

  [Fact]
  public async Task Role_targeted_discount_applies_via_an_admin_granted_extra_role()
  {
    var service = Make([RoleDiscount("vip", 5m)]);

    // token roles [] ∪ ExtraRoles ["vip"] — exactly what the Cost endpoints
    // hand to Materialize for a user with the admin-granted role
    string[] union = [.. Array.Empty<string>().Union(new[] { "vip" })];
    var result = await service.Materialize("user-1", union, Spec);

    result.SuccessOrDefault().Final.Should().Be(15m, "the vip discount applies via ExtraRoles");
  }

  [Fact]
  public async Task Role_targeted_discount_still_applies_via_a_token_role()
  {
    var service = Make([RoleDiscount("vip", 5m)]);

    string[] union = [.. new[] { "vip" }.Union(Array.Empty<string>())];
    var result = await service.Materialize("user-1", union, Spec);

    result.SuccessOrDefault().Final.Should().Be(15m, "token-role targeting is unchanged");
  }

  [Fact]
  public async Task The_union_deduplicates_and_never_double_applies()
  {
    var service = Make([RoleDiscount("vip", 5m)]);

    // the same role on the token AND in ExtraRoles
    string[] union = [.. new[] { "vip" }.Union(new[] { "vip" })];
    union.Should().HaveCount(1);
    var result = await service.Materialize("user-1", union, Spec);

    result.SuccessOrDefault().Final.Should().Be(15m);
    result.SuccessOrDefault().Discounts.Should().ContainSingle();
  }

  [Fact]
  public async Task Without_the_role_anywhere_the_discount_never_applies()
  {
    var service = Make([RoleDiscount("vip", 5m)]);

    var result = await service.Materialize("user-1", ["mortal"], Spec);

    result.SuccessOrDefault().Final.Should().Be(20m);
    result.SuccessOrDefault().Discounts.Should().BeEmpty();
  }

  [Fact]
  public async Task Extra_roles_also_price_the_specless_self_endpoint()
  {
    var service = Make([RoleDiscount("vip", 5m)]);

    var result = await service.Materialize("user-1", ["vip"], null);

    result.SuccessOrDefault().Final.Should().Be(15m, "Cost/self unions ExtraRoles too");
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

  private sealed class FakeCostPolicyRepository : ICostPolicyRepository
  {
    public Task<Result<IEnumerable<CostPolicyPrincipal>>> List() =>
      Task.FromResult((Result<IEnumerable<CostPolicyPrincipal>>)Array.Empty<CostPolicyPrincipal>());

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
