using CSharp_Result;
using Domain.Timings;

namespace Domain.Cost;

public interface ICostService
{
  Task<Result<IEnumerable<CostPrincipal>>> History();

  Task<Result<CostPrincipal>> Create(CostRecord record);

  Task<Result<CostPrincipal?>> GetCurrent();

  // spec = null prices without booking-aware policies (base + discounts only)
  Task<Result<MaterializedCost>> Materialize(string userId, string[] roles, BookingCostSpec? spec);

  // batch per-slot pricing: loads base cost + policies + candidate discounts
  // once and prices every time exactly like Materialize would
  Task<Result<IEnumerable<MaterializedCostSlot>>> MaterializeMany(
    string userId,
    string[] roles,
    DateOnly date,
    TrainDirection direction,
    IEnumerable<TimeOnly> times
  );

  // Policies (pricing rules)
  Task<Result<IEnumerable<CostPolicyPrincipal>>> ListPolicies();

  Task<Result<CostPolicyPrincipal>> CreatePolicy(CostPolicyRecord record);

  Task<Result<CostPolicyPrincipal?>> UpdatePolicy(Guid id, CostPolicyRecord record);

  Task<Result<Unit?>> DeletePolicy(Guid id);
}
