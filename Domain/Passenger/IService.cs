using CSharp_Result;

namespace Domain.Passenger;

public interface IPassengerService
{
  Task<Result<IEnumerable<PassengerPrincipal>>> Search(PassengerSearch search);

  Task<Result<Passenger?>> Get(string? userId, Guid id);

  Task<Result<PassengerPrincipal>> Create(string userId, PassengerRecord record);

  Task<Result<PassengerPrincipal?>> Update(string? userId, Guid id, PassengerRecord record);

  Task<Result<Unit?>> Delete(string? userId, Guid id);
}
