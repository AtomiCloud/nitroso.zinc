using CSharp_Result;
using Domain.Booking;
using Domain.Exceptions;
using Domain.Passenger;
using Domain.Withdrawal;

namespace Domain.User;

// PDPA right-to-erasure: deletes/anonymizes the user's personal data while
// retaining the financial records (bookings' revenue fields, wallets,
// transactions, payments, withdrawals) for the 5-year accounting retention.
// The surgical alternative to DELETE /User/{id}, which removes the row (and
// cascades) entirely.
public interface IUserWipeService
{
  // null = no such user (404); InvalidUserWipeOperationException (409) when
  // the wallet still holds money, a payout is in flight, or already wiped
  Task<Result<UserWipe?>> Wipe(string userId, string wipedById);
}

public class UserWipeService(
  IUserRepository userRepo,
  IPassengerRepository passengerRepo,
  IBookingRepository bookingRepo,
  IBookingStorage bookingStorage,
  IWithdrawalRepository withdrawalRepo,
  ITransactionManager transaction
) : IUserWipeService
{
  // payout states that still move money — the user must be fully paid out
  // (Completed / Rejected / Cancel) before erasure
  private static readonly WithdrawStatus[] InFlightStatuses =
  [
    WithdrawStatus.Pending,
    WithdrawStatus.Processing,
    WithdrawStatus.RequireManualIntervention,
  ];

  // the pinned anonymization shape: 'deleted-' + first 8 chars of the user
  // id — deterministic, so re-runs and audits can recognize a wiped row
  public static string AnonymizedUsername(string id) =>
    "deleted-" + id[..Math.Min(8, id.Length)];

  public Task<Result<UserWipe?>> Wipe(string userId, string wipedById)
  {
    return userRepo
      .GetById(userId)
      .ThenAwait(user =>
        user == null
          ? Task.FromResult((Result<UserWipe?>)(UserWipe?)null)
          : this.WipeExisting(user, wipedById)
      );
  }

  private async Task<Result<UserWipe?>> WipeExisting(User user, string wipedById)
  {
    var userId = user.Principal.Id;

    // refuse re-wipe: the stamp marks the row as already anonymized
    if (user.Principal.WipedAt != null)
      return new InvalidUserWipeOperationException(
        $"User '{userId}' has already been wiped",
        userId,
        "already_wiped"
      );

    // every cent must be paid out first — erasing while money is held would
    // orphan funds we can no longer attribute or return
    var w = user.Wallet.Record;
    if (w.Usable > 0 || w.WithdrawReserve > 0 || w.BookingReserve > 0)
      return new InvalidUserWipeOperationException(
        $"User '{userId}' still holds money in the wallet; pay the user out fully before wiping",
        userId,
        "wallet_not_empty"
      );

    var inFlight = await this.GuardNoInFlightWithdrawals(userId);
    if (inFlight.IsFailure())
      return inFlight.FailureOrDefault();

    // blob objects go FIRST: MinIO cannot join the DB transaction, and this
    // order is retryable in both directions — if a removal fails the DB is
    // untouched and the wipe can be re-run; if the DB transaction fails the
    // dangling Ticket references are re-collected and re-removed (Remove is
    // idempotent) on the next attempt. The reverse order could commit the
    // wipe stamp and then fail, leaving PII ticket PDFs in storage with no
    // retry path (re-wipe is refused).
    var removed = await bookingRepo
      .ListTicketKeys(userId)
      .ThenAwait(keys => this.RemoveTickets(keys));
    if (removed.IsFailure())
      return removed.FailureOrDefault();

    return await transaction
      .Start(() =>
        passengerRepo
          .DeleteByUser(userId)
          .ThenAwait(_ => bookingRepo.WipePersonalData(userId))
          .ThenAwait(_ => userRepo.Wipe(userId, wipedById))
          // the row was read above; vanishing mid-transaction is a hard fault
          .NullToError(userId)
      )
      .Then(
        p => (UserWipe?)new UserWipe { Id = p.Id, WipedAt = p.WipedAt!.Value },
        Errors.MapAll
      );
  }

  private async Task<Result<Unit>> GuardNoInFlightWithdrawals(string userId)
  {
    foreach (var status in InFlightStatuses)
    {
      var r = await withdrawalRepo.Search(
        new WithdrawalSearch
        {
          UserId = userId,
          Status = status,
          Limit = 1,
        }
      );
      if (r.IsFailure())
        return r.FailureOrDefault();
      if (r.Get().Any())
        return new InvalidUserWipeOperationException(
          $"User '{userId}' has a withdrawal in state '{status}'; it must settle before wiping",
          userId,
          "withdrawal_in_flight"
        );
    }

    return new Unit();
  }

  private async Task<Result<Unit>> RemoveTickets(string[] keys)
  {
    foreach (var key in keys)
    {
      var r = await bookingStorage.Remove(key);
      if (r.IsFailure())
        return r.FailureOrDefault();
    }

    return new Unit();
  }
}
