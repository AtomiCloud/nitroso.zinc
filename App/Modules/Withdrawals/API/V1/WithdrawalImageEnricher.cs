using App.Modules.Bookings.API.V1;
using CSharp_Result;
using Domain.Booking;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals.API.V1;

public interface IWithdrawalImageEnricher
{
  Task<Result<WithdrawalPrincipalRes>> Enrich(WithdrawalPrincipalRes booking);
  Task<Result<IEnumerable<WithdrawalPrincipalRes>>> Enrich(IEnumerable<WithdrawalPrincipalRes> booking);
  Task<Result<WithdrawalRes>> Enrich(WithdrawalRes booking);
}

public class WithdrawalImageEnricher(IWithdrawalStorage storage) : IWithdrawalImageEnricher
{
  public async Task<Result<WithdrawalPrincipalRes>> Enrich(WithdrawalPrincipalRes booking)
  {
    if (booking.Complete?.Receipt == null) return booking;

    return await storage.Get(booking.Complete.Receipt)
      .Select(link => booking with { Complete = booking.Complete with { Receipt = link } });
  }

  public async Task<Result<IEnumerable<WithdrawalPrincipalRes>>> Enrich(IEnumerable<WithdrawalPrincipalRes> booking)
  {
    var r = booking.Select(x => this.Enrich(x));
    var ret = await Task.WhenAll(r);
    return ret.ToResultOfSeq();
  }

  public async Task<Result<WithdrawalRes>> Enrich(WithdrawalRes booking)
  {
    return await this.Enrich(booking.Principal)
      .Select(principal => booking with { Principal = principal });
  }
}
