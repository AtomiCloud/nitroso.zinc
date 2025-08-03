using CSharp_Result;

namespace Domain.Discount;

public class DiscountService(IDiscountRepository repo) : IDiscountService
{
  public Task<Result<IEnumerable<DiscountPrincipal>>> Search(DiscountSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<DiscountPrincipal?>> Get(Guid id)
  {
    return repo.Get(id);
  }

  public Task<Result<DiscountPrincipal>> Create(DiscountRecord record, DiscountTarget target)
  {
    return repo.Create(record, target);
  }

  public Task<Result<DiscountPrincipal?>> Update(
    Guid id,
    DiscountStatus? status,
    DiscountRecord? record,
    DiscountTarget? target
  )
  {
    return repo.Update(id, status, record, target);
  }

  public Task<Result<Unit?>> Delete(Guid id)
  {
    return repo.Delete(id);
  }
}
