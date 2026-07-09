using CSharp_Result;

namespace Domain.Cost;

public interface ICostService
{
  Task<Result<IEnumerable<CostPrincipal>>> History();

  Task<Result<CostPrincipal>> Create(CostRecord record);

  Task<Result<CostPrincipal?>> GetCurrent();

  // spec = null prices without booking-aware policies (base + discounts only)
  Task<Result<MaterializedCost>> Materialize(string userId, string[] roles, BookingCostSpec? spec);

  // Policies (pricing rules)
  Task<Result<IEnumerable<CostPolicyPrincipal>>> ListPolicies();

  Task<Result<CostPolicyPrincipal>> CreatePolicy(CostPolicyRecord record);

  Task<Result<CostPolicyPrincipal?>> UpdatePolicy(Guid id, CostPolicyRecord record);

  Task<Result<Unit?>> DeletePolicy(Guid id);
}
