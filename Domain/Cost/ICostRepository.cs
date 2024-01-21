using CSharp_Result;

namespace Domain.Cost;

public interface ICostRepository
{
  Task<Result<IEnumerable<CostPrincipal>>> History();

  Task<Result<CostPrincipal>> Create(CostRecord record);

  Task<Result<CostPrincipal?>> GetCurrent();
}
