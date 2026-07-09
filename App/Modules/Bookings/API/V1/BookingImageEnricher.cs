using CSharp_Result;
using Domain.Booking;

namespace App.Modules.Bookings.API.V1;

public interface IBookingImageEnricher
{
  Task<Result<BookingPrincipalRes>> Enrich(BookingPrincipalRes booking);
  Task<Result<IEnumerable<BookingPrincipalRes>>> Enrich(IEnumerable<BookingPrincipalRes> booking);
  Task<Result<BookingRes>> Enrich(BookingRes booking);
}

public class BookingImageEnricher(
  IBookingStorage storage,
  ILogger<BookingImageEnricher> logger
) : IBookingImageEnricher
{
  public async Task<Result<BookingPrincipalRes>> Enrich(BookingPrincipalRes booking)
  {
    if (booking.TicketLink == null)
      return booking;

    // Producing a link must never fail the whole request: one unresolvable ticket
    // (e.g. a dangling reference or a transient storage error) would otherwise
    // sink an entire booking list via ToResultOfSeq. Degrade to no link rather
    // than a broken one, and log the key so the bad reference is discoverable.
    var key = booking.TicketLink;
    var link = await storage.Get(key);
    return link.Match(
      l => booking with { TicketLink = l },
      e =>
      {
        logger.LogWarning(e, "Failed to resolve ticket link for key {Key}", key);
        return booking with { TicketLink = null };
      }
    );
  }

  public async Task<Result<IEnumerable<BookingPrincipalRes>>> Enrich(
    IEnumerable<BookingPrincipalRes> booking
  )
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
