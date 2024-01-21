using CSharp_Result;

namespace Domain.Cost;

public interface ICostService
{
  Task<Result<IEnumerable<CostPrincipal>>> History();

  Task<Result<CostPrincipal>> Create(CostRecord record);

  Task<Result<CostPrincipal?>> GetCurrent();

  Task<Result<MaterializedCost>> Materialize(string userId, string[] roles);
}
