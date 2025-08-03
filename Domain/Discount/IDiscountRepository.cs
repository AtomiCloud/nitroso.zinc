using CSharp_Result;

namespace Domain.Discount;

public interface IDiscountRepository
{
  Task<Result<IEnumerable<DiscountPrincipal>>> Search(DiscountSearch search);

  Task<Result<DiscountPrincipal?>> Get(Guid id);

  Task<Result<DiscountPrincipal>> Create(DiscountRecord record, DiscountTarget target);

  Task<Result<DiscountPrincipal?>> Update(
    Guid id,
    DiscountStatus? status,
    DiscountRecord? record,
    DiscountTarget? target
  );

  Task<Result<Unit?>> Delete(Guid id);
}
