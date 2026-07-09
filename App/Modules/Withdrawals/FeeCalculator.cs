using CSharp_Result;
using Domain;

namespace App.Modules.Withdrawals;

public class FeeCalculator(IFeeRepository repo) : IFeeCalculator
{
  // no effective row = zero-zero: fees only exist once an admin queues one
  public Task<Result<FeeSpec>> Current(FeeType type)
  {
    return repo.GetCurrent(type)
      .Then(
        c =>
          c == null
            ? FeeSpec.None
            : new FeeSpec
            {
              Percentage = c.Percentage,
              FlatAmount = c.FlatAmount,
              Cap = c.Cap,
            },
        Errors.MapNone
      );
  }

  // Rounded to even cents so the ledger, the payout and the user-visible
  // numbers always agree; capped at the amount so a fee can never exceed
  // what is being moved (e.g. a flat fee on a tiny amount), and at the
  // admin-set Cap when one exists
  public Task<Result<decimal>> Compute(FeeType type, decimal amount)
  {
    return this.Current(type)
      .Then(
        spec =>
        {
          // degenerate amounts (zero/negative) can never carry a fee — and
          // guarding here keeps Math.Clamp's min<=max contract intact
          if (amount <= 0)
            return 0m;
          var raw = spec.FlatAmount + (amount * spec.Percentage / 100m);
          var fee = Math.Round(raw, 2, MidpointRounding.ToEven);
          return Math.Clamp(fee, 0m, Math.Min(amount, spec.Cap ?? decimal.MaxValue));
        },
        Errors.MapNone
      );
  }
}
