using CSharp_Result;
using Domain.Booking;

namespace App.Modules.Bookings.API.V1;

public interface IBookingImageEnricher
{
  Task<Result<BookingPrincipalRes>> Enrich(BookingPrincipalRes booking);
  Task<Result<IEnumerable<BookingPrincipalRes>>> Enrich(IEnumerable<BookingPrincipalRes> booking);
  Task<Result<BookingRes>> Enrich(BookingRes booking);
}

public class BookingImageEnricher(IBookingStorage storage) : IBookingImageEnricher
{
  public async Task<Result<BookingPrincipalRes>> Enrich(BookingPrincipalRes booking)
  {
    if (booking.TicketLink == null) return booking;

    return await storage.Get(booking.TicketLink)
      .Select(link => booking with { TicketLink = link });
  }

  public async Task<Result<IEnumerable<BookingPrincipalRes>>> Enrich(IEnumerable<BookingPrincipalRes> booking)
  {
    var r = booking.Select(x => this.Enrich(x));
    var ret = await Task.WhenAll(r);
    return ret.ToResultOfSeq();
  }

  public async Task<Result<BookingRes>> Enrich(BookingRes booking)
  {
    return await this.Enrich(booking.Principal)
      .Select(principal => booking with { Principal = principal });
  }
}
