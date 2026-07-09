using CSharp_Result;

namespace Domain.Cost;

public interface ICostPolicyRepository
{
  // all policies, newest first (the admin page shows the full list; pricing
  // filters applicability in the domain)
  Task<Result<IEnumerable<CostPolicyPrincipal>>> List();

  Task<Result<CostPolicyPrincipal>> Create(CostPolicyRecord record);

  // full-record replace; null when no such policy exists
  Task<Result<CostPolicyPrincipal?>> Update(Guid id, CostPolicyRecord record);

  // null when no such policy exists
  Task<Result<Unit?>> Delete(Guid id);
}
