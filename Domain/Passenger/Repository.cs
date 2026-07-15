using CSharp_Result;

namespace Domain.Passenger;

public interface IPassengerRepository
{
  Task<Result<IEnumerable<PassengerPrincipal>>> Search(PassengerSearch search);

  Task<Result<Passenger?>> Get(string? userId, Guid id);

  Task<Result<PassengerPrincipal>> Create(string userId, PassengerRecord record);

  Task<Result<PassengerPrincipal?>> Update(string? userId, Guid id, PassengerRecord record);

  Task<Result<Unit?>> Delete(string? userId, Guid id);

  // PDPA account wipe: hard-delete every saved passenger of this user
  // (FullName, PassportNumber, PassportExpiry, Gender — the PII core);
  // returns how many rows were removed (0 when the user had none)
  Task<Result<int>> DeleteByUser(string userId);
}
