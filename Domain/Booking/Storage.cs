using CSharp_Result;

namespace Domain.Booking;

public interface IBookingStorage
{
  Task<Result<string>> Save(Stream stream);

  Task<Result<string>> Get(string key);
}
