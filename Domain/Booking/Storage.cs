using CSharp_Result;

namespace Domain.Booking;

public interface IBookingStorage
{
  Task<Result<string>> Save(Stream stream);

  Task<Result<string>> Get(string key);

  // whether the stored object behind this key actually exists — used by the
  // ticket-health probe to surface dangling references without serving them
  Task<Result<bool>> Exists(string key);

  // permanently removes the stored object (PDPA wipe of ticket PDFs);
  // idempotent — a missing object is a success, not an error
  Task<Result<Unit>> Remove(string key);
}
