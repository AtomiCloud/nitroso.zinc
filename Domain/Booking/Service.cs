using CSharp_Result;

namespace Domain.Booking;

public class TrainBookingService(ITrainBookingRepository repo) : ITrainBookingService
{
  public Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Booking?>> Get(string? userId, Guid id)
  {
    return repo.Get(userId, id);
  }

  public Task<Result<BookingPrincipal>> Create(string? userId, BookingRecord record)
  {
    return repo.Create(userId, record);
  }

  public Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingRecord record)
  {
    return repo.Update(userId, id, null, record);
  }

  public Task<Result<BookingPrincipal?>> Complete(Guid id)
  {
    return repo.Update(null, id, new BookingStatus { Status = BookStatus.Completed, CompletedAt = DateTime.Now, },
      null);
  }

  public Task<Result<BookingPrincipal?>> Cancel(Guid id)
  {
    return repo.Update(null, id, new BookingStatus { Status = BookStatus.Cancelled, CompletedAt = DateTime.Now, },
      null);
  }

  public Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    return repo.Delete(userId, id);
  }


  public Task<Result<IEnumerable<BookingCount>>> Count()
  {
    var singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore");
    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.Now, singapore);
    var dateNow = DateOnly.FromDateTime(now);
    var timeNow = TimeOnly.FromDateTime(now);

    return repo.Count(dateNow, timeNow);
  }
}
