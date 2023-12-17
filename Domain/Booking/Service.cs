using CSharp_Result;
using Microsoft.Extensions.Logging;

namespace Domain.Booking;

public class BookingService(IBookingRepository repo, IBookingStorage fileRepository, ILogger<BookingService> logger)
  : IBookingService
{
  public Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<Booking?>> Get(string? userId, Guid id)
  {
    return repo.Get(userId, id);
  }

  public Task<Result<BookingPrincipal>> Create(string userId, BookingRecord record)
  {
    return repo.Create(userId, record);
  }

  public Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingRecord record)
  {
    return repo.Update(userId, id, null, record, null);
  }

  public Task<Result<BookingPrincipal?>> Buying(Guid id)
  {
    return repo.Update(null, id, new BookingStatus() { Status = BookStatus.Buying, CompletedAt = null }, null, null);
  }

  public async Task<Result<BookingPrincipal?>> Complete(Guid id, Stream file)
  {
    return await fileRepository.Save(file)
      .ThenAwait(x => repo.Update(null, id,
        new BookingStatus { Status = BookStatus.Completed, CompletedAt = DateTime.UtcNow },
        null, new BookingComplete() { Ticket = x, }));
  }

  public Task<Result<BookingPrincipal?>> Cancel(Guid id)
  {
    return repo.Update(null, id, new BookingStatus { Status = BookStatus.Cancelled, CompletedAt = DateTime.UtcNow, },
      null, null);
  }

  public Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    return repo.Delete(userId, id);
  }


  public Task<Result<IEnumerable<BookingCount>>> Count()
  {
    var singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore");
    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singapore);
    var dateNow = DateOnly.FromDateTime(now);
    var timeNow = TimeOnly.FromDateTime(now);

    logger.LogInformation("Get booking count after {Date} {Time}", dateNow, timeNow);

    return repo.Count(dateNow, timeNow);
  }
}
