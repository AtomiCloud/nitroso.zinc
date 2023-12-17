using App.Modules.Common;
using App.StartUp.BlockStorage;
using App.StartUp.Registry;
using CSharp_Result;
using Domain.Booking;

namespace App.Modules.Bookings.Data;

public class BookingStorage(IFileRepository file) : IBookingStorage
{
  public Task<Result<string>> Save(Stream stream)
  {
    return file.Save(BlockStorages.Main, "bookings", Guid.NewGuid().ToString(), stream, true);
  }

  public Task<Result<string>> Get(string key)
  {
    return file.SignedLink(BlockStorages.Main, key, 60 * 60);
  }
}
