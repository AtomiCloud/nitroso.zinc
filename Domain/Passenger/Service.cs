using CSharp_Result;

namespace Domain.Passenger;

public class PassengerService(IPassengerRepository repo) : IPassengerService
{
  public Task<Result<IEnumerable<PassengerPrincipal>>> Search(PassengerSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Passenger?>> Get(string? userId, Guid id)
  {
    return repo.Get(userId, id);
  }

  public Task<Result<PassengerPrincipal>> Create(string userId, PassengerRecord record)
  {
    return repo.Create(userId, record);
  }

  public Task<Result<PassengerPrincipal?>> Update(string? userId, Guid id, PassengerRecord record)
  {
    return repo.Update(userId, id, record);
  }

  public Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    return repo.Delete(userId, id);
  }
}
