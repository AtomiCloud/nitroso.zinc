using CSharp_Result;

namespace Domain.Booking;

public interface IBookingStorage
{
  Task<Result<string>> Save(Stream stream);

  Task<Result<string>> Get(string key);

  // whether the stored object behind this key actually exists — used by the
  // ticket-health probe to surface dangling references without serving them
  Task<Result<bool>> Exists(string key);
}
