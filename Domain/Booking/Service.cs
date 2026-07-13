using CSharp_Result;
using Domain.Exceptions;
using Domain.Timings;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;
using Microsoft.Extensions.Logging;

namespace Domain.Booking;

public class BookingService(
  IBookingRepository repo,
  IBookingStorage fileRepo,
  IWalletRepository walletRepo,
  ITransactionRepository transactionRepo,
  ITransactionManager transaction,
  ITransactionGenerator transactionGenerator,
  IFeeCalculator feeCalculator,
  IBookingTerminatorRepository terminatorRepository,
  IBookingCdcRepository cdcRepository,
  IBookingNotificationService notificationService,
  IPrioritySettingsRepository prioritySettingsRepo,
  IPriorityAccessRepository priorityAccessRepo,
  IUserRepository userRepo,
  ILogger<BookingService> logger,
  TimeProvider? timeProvider = null
) : IBookingService
{
  private TimeProvider Clock { get; } = timeProvider ?? TimeProvider.System;

  public Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search)
  {
    return repo.Search(search);
  }

  public Task<Result<int>> SearchCount(BookingSearch search)
  {
    return repo.SearchCount(search);
  }

  public Task<Result<BookingQueuePosition?>> QueuePosition(string? userId, Guid id)
  {
    return repo.QueuePosition(userId, id);
  }

  public Task<Result<IEnumerable<BookingStatRow>>> Stats(BookingStatsQuery query)
  {
    return repo.Stats(query);
  }

  public Task<Result<IEnumerable<BookingPrincipal>>> ListRefunds(DateTime referenceTime)
  {
    var singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore");
    var now = TimeZoneInfo.ConvertTimeFromUtc(referenceTime, singapore);
    var dateNow = DateOnly.FromDateTime(now);
    var timeNow = TimeOnly.FromDateTime(now);

    logger.LogInformation("Get bookings before {Date} {Time}", dateNow, timeNow);

    return repo.RefundList(dateNow, timeNow);
  }

  public Task<Result<Booking?>> Get(string? userId, Guid id)
  {
    return repo.Get(userId, id);
  }

  // When user creates a booking
  public Task<Result<BookingPrincipal>> Create(
    string userId,
    decimal cost,
    BookingRecord record,
    BookingPriceBreakdown? breakdown = null
  )
  {
    // Defense in depth: API Purchase checks before pricing, and this domain
    // boundary checks again before opening a transaction or touching a wallet.
    var timing = BookingPurchaseTiming.Validate(record, this.Clock.GetUtcNow());
    if (!timing.IsSuccess())
      return Task.FromResult((Result<BookingPrincipal>)timing.FailureOrDefault());

    return transaction
      .Start(
        () =>
          walletRepo
            .GetByUserId(userId)
            .NullToError(userId)
            // The user/wallet lookup can cross the exact cutoff. Re-read the
            // clock inside the transaction immediately before BookStart so a
            // newly expired request cannot reserve any money.
            .Then(w =>
              BookingPurchaseTiming
                .Validate(record, this.Clock.GetUtcNow())
                .Then(_ => w, Errors.MapNone)
            )
            .ThenAwait(x => walletRepo.BookStart(x.Principal.Id, cost))
            .NullToError(userId)
            .ThenAwait(w =>
              transactionRepo.Create(w.Id, transactionGenerator.CreateBooking(cost, record))
            )
            .ThenAwait(x => repo.Create(userId, x.Id, record, breakdown))
      )
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("create"));
  }

  // updating a booking, should only be allowed by admin
  public Task<Result<BookingPrincipal?>> Update(string? userId, Guid id, BookingRecord record)
  {
    return repo.Update(userId, id, null, record, null)
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("update"));
  }

  // This is an attempt to get what should be reserved next. This is a "Get".
  public Task<Result<BookingPrincipal?>> Reserve(
    TrainDirection direction,
    DateOnly date,
    TimeOnly time
  )
  {
    return repo.Reserve(direction, date, time);
  }

  // This marks the ticket in the buying status. Guarded and wrapped in a
  // transaction like the recovery transitions: the buyer only ever drives a
  // 'Pending' booking into 'Buying', so a blind write here (from a stale
  // tin/admin caller) could otherwise overwrite a terminal/recovered/completed
  // booking back into 'Buying' — stranding a collected ticket outside every
  // automation path (Sweep lists only 'Recovering', RefundList touches only
  // 'Pending'). The status read + write run inside the RepeatableRead
  // transaction so the guard cannot go stale against a concurrent transition,
  // and the 'BookingNumber == null' guard refuses a booking that already
  // captured a ticket (reserve collected)
  public Task<Result<BookingPrincipal?>> Buying(Guid id)
  {
    return transaction.Start(
      () =>
        repo.Get(null, id)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            b =>
            {
              if (
                b.Principal.Status.Status == BookStatus.Pending
                && b.Principal.Complete.BookingNumber == null
              )
                return Task.FromResult((Result<int>)0);
              var r = new InvalidBookingOperationException(
                "Buying requires an uncaptured booking in 'Pending' Status",
                b.Principal.Status.Status,
                BookingOperations.Buy
              );
              return Task.FromResult((Result<int>)r);
            }
          )
          .ThenAwait(_ =>
            repo.Update(
              null,
              id,
              new BookingStatus() { Status = BookStatus.Buying, CompletedAt = null },
              null,
              null
            )
          )
    );
  }

  // Revert moves a stuck Buying booking back to Pending so it re-enters the
  // demand pool for another attempt (e.g. after a transient KTMB failure like
  // an insufficient wallet balance, where no ticket was ever bought). The guard
  // and the status write run in ONE transaction and only fire when the booking
  // is still Buying AND uncaptured (BookingNumber == null): this makes it atomic
  // — it can never clobber a booking that completed concurrently, nor revert one
  // that already captured a KTMB ticket into re-buying (the corruption + double
  // -buy hazards the old unguarded reverter caused). Status-only: no money moves
  // (both Pending and Buying hold the amount in BookingReserve).
  //
  // force = false (automation, e.g. helium's reverter cron): additionally
  // requires the booking to have sat in Buying for more than StuckThreshold,
  // so the cron can never race the buyer's live purchase window. Bookings
  // predating the LastBuyingAt column (null stamp) are refused — never guess.
  // force = true (admin UI): no age check, and RequireManualIntervention is
  // also accepted; a human has judged the booking safe to re-expose.
  public static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(5);

  public Task<Result<BookingPrincipal?>> Revert(Guid id, bool force)
  {
    return transaction.Start(
      () =>
        repo.Get(null, id)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            b =>
            {
              var status = b.Principal.Status.Status;
              var uncaptured = b.Principal.Complete.BookingNumber == null;
              var allowed = force
                ? (status == BookStatus.Buying || status == BookStatus.RequireManualIntervention)
                  && uncaptured
                : status == BookStatus.Buying
                  && uncaptured
                  && b.Principal.Status.LastBuyingAt != null
                  && DateTime.UtcNow - b.Principal.Status.LastBuyingAt > StuckThreshold;
              if (allowed)
                return Task.FromResult((Result<int>)0);
              var r = new InvalidBookingOperationException(
                force
                  ? "Revert requires an uncaptured booking in 'Buying' or 'RequireManualIntervention' Status"
                  : "Revert (non-force) requires an uncaptured booking stuck in 'Buying' Status for more than 5 minutes",
                b.Principal.Status.Status,
                BookingOperations.Revert
              );
              return Task.FromResult((Result<int>)r);
            }
          )
          .ThenAwait(_ =>
            repo.Update(
              null,
              id,
              new BookingStatus() { Status = BookStatus.Pending, CompletedAt = null },
              null,
              null
            )
          )
    );
  }

  // RecoverRevert recycles a 'Recovering' booking back to 'Pending' so it
  // re-enters the demand pool for another purchase attempt, counting the
  // attempt in RecoveryRetries. Status-only: no money moves (both Recovering
  // and Pending hold the amount in BookingReserve). The guard, the counter
  // increment and the status write run in ONE transaction so a concurrent
  // transition (or a concurrent RecoverRevert) can never lose an increment or
  // clobber a booking that completed meanwhile. Once the counter reaches
  // maxRetries the recycle is refused with RecoveryRetriesExhaustedException
  // (a distinguishable failure, not a transition): the booking stays parked in
  // 'Recovering' for a human to resolve via Duplicate/ManualIntervention.
  public Task<Result<BookingPrincipal?>> RecoverRevert(Guid id, int maxRetries)
  {
    return transaction.Start(
      () =>
        repo.Get(null, id)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            b =>
            {
              if (
                b.Principal.Status.Status != BookStatus.Recovering
                || b.Principal.Complete.BookingNumber != null
              )
              {
                var r = new InvalidBookingOperationException(
                  "Recover-revert requires an uncaptured booking in 'Recovering' Status",
                  b.Principal.Status.Status,
                  BookingOperations.RecoverRevert
                );
                return Task.FromResult((Result<int>)r);
              }

              if (b.Principal.RecoveryRetries >= maxRetries)
              {
                var r = new RecoveryRetriesExhaustedException(
                  id.ToString(),
                  b.Principal.RecoveryRetries,
                  maxRetries
                );
                return Task.FromResult((Result<int>)r);
              }

              return Task.FromResult((Result<int>)0);
            }
          )
          .ThenAwait(_ => repo.IncrementRecoveryRetries(id))
          .NullToError(id.ToString())
          .ThenAwait(_ =>
            repo.Update(
              null,
              id,
              new BookingStatus() { Status = BookStatus.Pending, CompletedAt = null },
              null,
              null
            )
          )
    );
  }

  // This parks a buying booking whose purchase hit a KTMB conflict (e.g.
  // duplicate passport) until the recoverer resolves it. Wrapped in a
  // transaction so the guard read cannot go stale against a concurrent
  // transition (a blind write here could overwrite a terminal status)
  public Task<Result<BookingPrincipal?>> Recovering(Guid id)
  {
    return transaction.Start(
      () =>
        repo.Get(null, id)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            b =>
            {
              if (b.Principal.Status.Status == BookStatus.Buying)
                return Task.FromResult((Result<int>)0);
              var r = new InvalidBookingOperationException(
                "Recovering requires booking to be in 'Buying' Status",
                b.Principal.Status.Status,
                BookingOperations.Recover
              );
              return Task.FromResult((Result<int>)r);
            }
          )
          .ThenAwait(_ =>
            repo.Update(
              null,
              id,
              new BookingStatus() { Status = BookStatus.Recovering, CompletedAt = null },
              null,
              null
            )
          )
    );
  }

  // This parks a booking that automation must never touch (e.g. ledger moved
  // but status inconsistent); a human resolves it out-of-band. Wrapped in a
  // transaction so the guard read cannot go stale against a concurrent
  // transition (a blind write here could resurrect a refunded booking into
  // a refund-eligible state and double-refund the pooled reserve)
  public Task<Result<BookingPrincipal?>> ManualIntervention(Guid id)
  {
    return transaction.Start(
      () =>
        repo.Get(null, id)
          .NullToError(id.ToString())
          .DoAwait(
            DoType.MapErrors,
            b =>
            {
              var status = b.Principal.Status.Status;
              if (
                status
                is not (
                  BookStatus.Completed
                  or BookStatus.Cancelled
                  or BookStatus.Refunded
                  or BookStatus.Terminated
                  or BookStatus.Duplicate
                )
              )
                return Task.FromResult((Result<int>)0);
              var r = new InvalidBookingOperationException(
                "Manual intervention requires booking to be in a non-terminal Status",
                status,
                BookingOperations.ManualIntervention
              );
              return Task.FromResult((Result<int>)r);
            }
          )
          .ThenAwait(_ =>
            repo.Update(
              null,
              id,
              new BookingStatus() { Status = BookStatus.RequireManualIntervention, CompletedAt = null },
              null,
              null
            )
          )
          // Error if Null
          .NullToError(id.ToString())
          // Re-retrieve full booking (with user) for the notification
          .ThenAwait(x => repo.Get(null, x.Id))
      )
      .NullToError(id.ToString())
      .DoAwait(DoType.Ignore, x => notificationService.NotifyBookingManualIntervention(x)
        .Match(s =>
          {
            logger.LogInformation("Notify booking manual intervention successfully");
            return s;
          },
          exception =>
          {
            logger.LogError(exception, "Failed to notify booking manual intervention");
            return new Unit();
          }), Errors.MapNone)
      .Then(BookingPrincipal? (x) => x.Principal, Errors.MapNone);
  }

  // This marks the ticket in the bought status, need to move $$
  // CompleteNoCollect is a deliberate ADMIN BYPASS for resolving older/corrupted
  // RequireManualIntervention bookings: it delivers an admin-supplied ticket and
  // marks the booking Completed but does NO BookEnd and writes NO ledger row, so
  // the held BookingReserve is left untouched — the ledger is knowingly left
  // inconsistent and is reconciled MANUALLY afterwards (per the operator's
  // decision; see the money-flow note below).
  //
  // KNOWN CAVEAT (accepted): a Completed booking normally implies its reserve was
  // collected, and Terminate assumes that. A CompleteNoCollect booking violates
  // that invariant (reserve still held), so a subsequent Terminate on it would
  // fabricate a refund + strand the reserve. This is tolerated because it is an
  // admin-only bypass for edge/corrupted bookings whose ledger is patched by hand;
  // do NOT extend this method to normal flows.
  //
  // Unlike the other guards it intentionally does NOT require BookingNumber==null:
  // the corrupted bookings this fixes may already carry a (stale) captured ticket,
  // and since no money moves there is no double-collect risk in overwriting it.
  // Guarded to the RequireManualIntervention parking state only.
  public Task<Result<BookingPrincipal?>> CompleteNoCollect(Guid id,
    string bookingNo,
    string ticketNo,
    Stream file)
  {
    return fileRepo
      .Save(file)
      .ThenAwait(fileId => transaction.Start(
          () =>
            repo.Get(null, id)
              .NullToError(id.ToString())
              .DoAwait(
                DoType.MapErrors,
                b =>
                {
                  if (b.Principal.Status.Status == BookStatus.RequireManualIntervention)
                    return Task.FromResult((Result<int>)0);
                  var r = new InvalidBookingOperationException(
                    "Complete-without-collect requires a booking in 'RequireManualIntervention' Status",
                    b.Principal.Status.Status,
                    BookingOperations.CompleteNoCollect
                  );
                  return Task.FromResult((Result<int>)r);
                }
              )
              // deliberately NO BookEnd and NO transaction row: the reserve stays
              // held for manual settlement (per the admin's chosen resolution)
              .ThenAwait(_ =>
                repo.Update(
                  null,
                  id,
                  new BookingStatus
                  {
                    Status = BookStatus.Completed,
                    CompletedAt = DateTime.UtcNow,
                  },
                  null,
                  new BookingComplete
                  {
                    Ticket = fileId,
                    BookingNumber = bookingNo,
                    TicketNumber = ticketNo,
                  }
                )
              )
              .NullToError(id.ToString())
              .ThenAwait(x => repo.Get(null, x.Id))
        )
      )
      .NullToError(id.ToString())
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"))
      .DoAwait(DoType.Ignore, x => notificationService.NotifyBookingCompleted(x)
          .Match(s => s,
            exception =>
            {
              logger.LogError(exception, "Failed to notify booking completed (no-collect)");
              return new Unit();
            }), Errors.MapNone)
      .Then(BookingPrincipal? (x) => x.Principal, Errors.MapNone);
  }

  public Task<Result<BookingPrincipal?>> Complete(Guid id,
    string bookingNo,
    string ticketNo,
    Stream file)
  {
    return fileRepo
      .Save(file)
      .ThenAwait(fileId => transaction.Start(
          () =>
            // get booking
            repo.Get(null, id)
              // error if null
              .NullToError(id.ToString())
              // block completing from terminal/parked states or a booking that
              // already captured a ticket: the reserve is pooled per-wallet, so a
              // second collect would silently take other bookings' holds
              .DoAwait(
                DoType.MapErrors,
                b =>
                {
                  if (
                    b.Principal.Status.Status
                      is BookStatus.Pending
                        or BookStatus.Buying
                        or BookStatus.Recovering
                        or BookStatus.RequireManualIntervention
                    && b.Principal.Complete.BookingNumber == null
                  )
                    return Task.FromResult((Result<int>)0);
                  var r = new InvalidBookingOperationException(
                    "Completion requires an uncompleted booking in 'Pending', 'Buying', 'Recovering' or 'RequireManualIntervention' Status",
                    b.Principal.Status.Status,
                    BookingOperations.Complete
                  );
                  return Task.FromResult((Result<int>)r);
                }
              )
              // move the money
              .DoAwait(
                DoType.MapErrors,
                b =>
                  walletRepo
                    .BookEnd(b.Wallet.Id, 0, b.Transaction.Record.Amount)
                    .NullToError(b.Wallet.Id.ToString())
              )
              // Create transaction from original transaction and booking
              .ThenAwait(b =>
                transactionRepo.Create(
                  b.Wallet.Id,
                  transactionGenerator.CompleteBooking(b.Transaction.Record, b.Principal.Record)
                )
              )
              // Update booking
              .ThenAwait(x =>
                repo.Update(
                  null,
                  id,
                  new BookingStatus
                  {
                    Status = BookStatus.Completed,
                    CompletedAt = DateTime.UtcNow,
                  },
                  null,
                  new BookingComplete
                  {
                    Ticket = fileId,
                    BookingNumber = bookingNo,
                    TicketNumber = ticketNo,
                  }
                )
              )
              // Error if Null
              .NullToError(id.ToString())
              // Re-retrieve full booking
              .ThenAwait(x => repo.Get(null, x.Id))
        )
      )
      .NullToError(id.ToString())
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"))
      .DoAwait(DoType.Ignore, x => notificationService.NotifyBookingCompleted(x)
          .Match(s => s,
            exception =>
            {
              logger.LogError(exception, "Failed to notify booking completed");
              return new Unit();
            }), Errors.MapNone)
      .Then(BookingPrincipal? (x) => x.Principal, Errors.MapNone);

  }

  // AttachTicket repairs the ticket artefacts of an already-Completed booking
  // (e.g. the upload was lost and the stored Ticket key dangles, or the ticket
  // was completed with the wrong file). Money moved when the booking completed,
  // so this is strictly status-preserving: NO BookEnd, NO ledger row, NO status
  // change — only the Completed row's ticket fields are touched.
  //
  // The file is saved (and its persistence verified by the storage layer)
  // BEFORE the transaction opens, exactly like Complete, so a failed upload can
  // never commit a dangling reference. Overwriting an existing non-null Ticket
  // reference is allowed — that IS the dangling-ref repair. The KTMB
  // identifiers are different: they are facts about the captured reservation,
  // so a provided bookingNo/ticketNo may only backfill a missing (null) value;
  // conflicting with an existing non-null value is refused rather than silently
  // rewriting reservation identity.
  public Task<Result<BookingPrincipal?>> AttachTicket(
    Guid id,
    string? bookingNo,
    string? ticketNo,
    Stream file
  )
  {
    return fileRepo
      .Save(file)
      .ThenAwait(fileId => transaction.Start(
          () =>
            repo.Get(null, id)
              .NullToError(id.ToString())
              .DoAwait(
                DoType.MapErrors,
                b =>
                {
                  if (b.Principal.Status.Status != BookStatus.Completed)
                  {
                    var r = new InvalidBookingOperationException(
                      "Attaching a ticket requires a booking in 'Completed' Status",
                      b.Principal.Status.Status,
                      BookingOperations.AttachTicket
                    );
                    return Task.FromResult((Result<int>)r);
                  }

                  var existing = b.Principal.Complete;
                  var bookingNoConflicts =
                    bookingNo != null
                    && existing.BookingNumber != null
                    && bookingNo != existing.BookingNumber;
                  var ticketNoConflicts =
                    ticketNo != null
                    && existing.TicketNumber != null
                    && ticketNo != existing.TicketNumber;
                  if (bookingNoConflicts || ticketNoConflicts)
                  {
                    var r = new InvalidBookingOperationException(
                      "Attaching a ticket may only backfill missing booking/ticket numbers; it cannot overwrite existing ones with different values",
                      b.Principal.Status.Status,
                      BookingOperations.AttachTicket
                    );
                    return Task.FromResult((Result<int>)r);
                  }

                  return Task.FromResult((Result<int>)0);
                }
              )
              // no money movement and no status write: repair the ticket
              // reference and backfill any missing identifiers only
              .ThenAwait(b =>
                repo.Update(
                  null,
                  id,
                  null,
                  null,
                  new BookingComplete
                  {
                    Ticket = fileId,
                    BookingNumber = b.Principal.Complete.BookingNumber ?? bookingNo,
                    TicketNumber = b.Principal.Complete.TicketNumber ?? ticketNo,
                  }
                )
              )
        )
      )
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("update"));
  }

  // Cheap single-booking probe for the admin UI: does this booking carry a
  // ticket reference at all, and does that reference resolve to a real stored
  // object. Never serves the object — pairs with AttachTicket to find and fix
  // dangling references.
  public Task<Result<BookingTicketHealth?>> TicketHealth(Guid id)
  {
    return repo.Get(null, id)
      .ThenAwait(b =>
      {
        if (b == null)
          return Task.FromResult((Result<BookingTicketHealth?>)(BookingTicketHealth?)null);
        var key = b.Principal.Complete.Ticket;
        if (key == null)
          return Task.FromResult(
            (Result<BookingTicketHealth?>)
              new BookingTicketHealth { HasRef = false, RefValid = false }
          );
        return fileRepo
          .Exists(key)
          .Then(
            BookingTicketHealth? (exists) =>
              new BookingTicketHealth { HasRef = true, RefValid = exists },
            Errors.MapNone
          );
      });
  }

  // When user cancels the tickets before booking succeeded
  public Task<Result<BookingPrincipal?>> Cancel(string? userId, Guid id)
  {
    return transaction
      .Start(
        () =>
          repo
          // get booking
          .Get(userId, id)
            // error if null
            .NullToError(id.ToString())
            // block cancelling if status is NOT pending
            .DoAwait(
              DoType.MapErrors,
              b =>
              {
                if (
                  b.Principal.Status.Status == BookStatus.Pending
                  || (
                    b.Principal.Status.Status == BookStatus.RequireManualIntervention
                    && b.Principal.Complete.BookingNumber == null
                  )
                )
                  return Task.FromResult((Result<int>)0);
                var r = new InvalidBookingOperationException(
                  "Cancellation requires an uncaptured booking in 'Pending' or 'RequireManualIntervention' Status",
                  b.Principal.Status.Status,
                  BookingOperations.Cancel
                );
                return Task.FromResult((Result<int>)r);
              }
            )
            // move the money
            .DoAwait(
              DoType.MapErrors,
              b =>
                walletRepo
                  .BookEnd(b.Wallet.Id, b.Transaction.Record.Amount, 0)
                  .NullToError(b.Wallet.Id.ToString())
            )
            // Create transaction
            .DoAwait(
              DoType.MapErrors,
              b =>
                transactionRepo.Create(
                  b.Wallet.Id,
                  transactionGenerator.CancelBooking(b.Transaction.Record, b.Principal.Record)
                )
            )
            // return the priority fee: the user backed out before delivery,
            // so the paid-for queue jump was never consumed
            .DoAwait(DoType.MapErrors, this.RefundPriorityFeeIfAny)
            // update the booking
            .ThenAwait(x =>
              repo.Update(
                userId,
                id,
                new BookingStatus { Status = BookStatus.Cancelled, CompletedAt = DateTime.UtcNow },
                null,
                null
              )
            )
            // Error if Null
            .NullToError(id.ToString())
            // Re-retrieve full booking
            .ThenAwait(x => repo.Get(null, x.Id))
      )
      .NullToError(id.ToString())
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"))
      .DoAwait(DoType.Ignore, x => notificationService.NotifyBookingCancelled(x)
        .Match(s =>
          {
            logger.LogInformation("Notify booking cancelled successfully");
            return s;
          },
          exception =>
          {
            logger.LogError(exception, "Failed to notify booking cancelled");
            return new Unit();
          }), Errors.MapNone)
      .Then(BookingPrincipal? (x) => x.Principal, Errors.MapNone);
  }

  // When the recoverer confirms the user already holds this ticket via another
  // channel: full refund (same money flow as Cancel), terminal 'Duplicate' status
  public Task<Result<BookingPrincipal?>> Duplicate(Guid id)
  {
    return transaction
      .Start(
        () =>
          repo
          // get booking
          .Get(null, id)
            // error if null
            .NullToError(id.ToString())
            // block marking duplicate unless recovering (automated) or parked for
            // manual intervention (human-approved refund); a booking that already
            // captured a ticket has collected its reserve and must never be refunded
            .DoAwait(
              DoType.MapErrors,
              b =>
              {
                if (
                  b.Principal.Status.Status
                    is BookStatus.Recovering
                      or BookStatus.RequireManualIntervention
                  && b.Principal.Complete.BookingNumber == null
                )
                  return Task.FromResult((Result<int>)0);
                var r = new InvalidBookingOperationException(
                  "Marking duplicate requires an uncompleted booking in 'Recovering' or 'RequireManualIntervention' Status",
                  b.Principal.Status.Status,
                  BookingOperations.Duplicate
                );
                return Task.FromResult((Result<int>)r);
              }
            )
            // move the money
            .DoAwait(
              DoType.MapErrors,
              b =>
                walletRepo
                  .BookEnd(b.Wallet.Id, b.Transaction.Record.Amount, 0)
                  .NullToError(b.Wallet.Id.ToString())
            )
            // Create transaction
            .DoAwait(
              DoType.MapErrors,
              b =>
                transactionRepo.Create(
                  b.Wallet.Id,
                  transactionGenerator.DuplicateBooking(b.Transaction.Record, b.Principal.Record)
                )
            )
            // return the priority fee: no ticket was delivered by us, so the
            // paid-for queue jump was never consumed — same policy as Refund
            // and Cancel (Duplicate is terminal; keeping the fee would be a
            // silent, unledgered retention)
            .DoAwait(DoType.MapErrors, this.RefundPriorityFeeIfAny)
            // update the booking
            .ThenAwait(x =>
              repo.Update(
                null,
                id,
                new BookingStatus { Status = BookStatus.Duplicate, CompletedAt = DateTime.UtcNow },
                null,
                null
              )
            )
            // Error if Null
            .NullToError(id.ToString())
            // Re-retrieve full booking
            .ThenAwait(x => repo.Get(null, x.Id))
      )
      .NullToError(id.ToString())
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"))
      .DoAwait(DoType.Ignore, x => notificationService.NotifyBookingDuplicate(x)
        .Match(s =>
          {
            logger.LogInformation("Notify booking duplicate successfully");
            return s;
          },
          exception =>
          {
            logger.LogError(exception, "Failed to notify booking duplicate");
            return new Unit();
          }), Errors.MapNone)
      .Then(BookingPrincipal? (x) => x.Principal, Errors.MapNone);
  }

  // When users cancel the tickets after booking succeeded
  public Task<Result<BookingPrincipal>> Terminate(string? userId, Guid id, DateTime referenceTime)
  {
    return transaction
      .Start(
        () =>
          repo
          // get booking
          .Get(userId, id)
            // error if null
            .NullToError(id.ToString())
            // block terminating if status is NOT complete
            .DoAwait(
              DoType.MapErrors,
              b =>
              {
                if (b.Principal.Status.Status == BookStatus.Completed)
                  return Task.FromResult((Result<int>)0);
                var r = new InvalidBookingOperationException(
                  "Termination require booking to be in 'Completed' Status",
                  b.Principal.Status.Status,
                  BookingOperations.Terminate
                );
                return Task.FromResult((Result<int>)r);
              }
            )
            // block terminating if ticket is 30 min before departure
            .DoAwait(
              DoType.MapErrors,
              b =>
              {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
                var t = b.Principal.Record.Date.ToZonedDateTime(b.Principal.Record.Time, tz);
                if (referenceTime < t)
                  return Task.FromResult((Result<int>)0);
                var r = new InvalidBookingOperationException(
                  $"Cannot terminate booking past buffer time before departure",
                  b.Principal.Status.Status,
                  BookingOperations.Terminate
                );
                return Task.FromResult((Result<int>)r);
              }
            )
            // the fee engine decides the termination penalty; the refund is
            // the remainder — computed ONCE so the wallet movement and the
            // ledger description always agree
            .ThenAwait(b =>
              feeCalculator
                .Compute(FeeType.Termination, b.Transaction.Record.Amount)
                .Then(
                  fee => (Booking: b, Fee: fee, Refund: b.Transaction.Record.Amount - fee),
                  Errors.MapNone
                )
            )
            // move the money
            .DoAwait(
              DoType.MapErrors,
              t =>
                walletRepo
                  .Deposit(t.Booking.Wallet.Id, t.Refund)
                  .NullToError(t.Booking.Wallet.Id.ToString())
            )
            // Create transaction
            .DoAwait(
              DoType.MapErrors,
              t =>
                transactionRepo.Create(
                  t.Booking.Wallet.Id,
                  transactionGenerator.TerminateBooking(t.Booking.Principal.Record, t.Refund, t.Fee)
                )
            )
            // update the booking
            .ThenAwait(x =>
              repo.Update(
                userId,
                id,
                new BookingStatus { Status = BookStatus.Terminated, CompletedAt = DateTime.UtcNow },
                null,
                null
              )
            )
            // Error if Null
            .NullToError(id.ToString())
            // Re-retrieve full booking
            .ThenAwait(x => repo.Get(null, x.Id))
      )
      .NullToError(id.ToString())
      // terminate the booking in KTMB through tin
      .DoAwait(
        DoType.Ignore,
        b =>
          terminatorRepository.Terminate(
            new BookingTermination(b.Principal.Complete.BookingNumber!, b.Principal.Complete.TicketNumber!)
          )
      )
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"))
      .DoAwait(DoType.Ignore, notificationService.NotifyBookingTerminated)
      .Then(x => x.Principal, Errors.MapNone);
  }

  // When the system cancels the tickets after failed
  public Task<Result<BookingPrincipal>> Refund(Guid id)
  {
    return transaction
      .Start(
        () =>
          repo
          // get booking
          .Get(null, id)
            // error if null
            .NullToError(id.ToString())
            // block terminating if status is NOT complete
            .DoAwait(
              DoType.MapErrors,
              b =>
              {
                if (b.Principal.Status.Status == BookStatus.Pending)
                  return Task.FromResult((Result<int>)0);
                var r = new InvalidBookingOperationException(
                  "Refund require booking to be in 'Pending' Status",
                  b.Principal.Status.Status,
                  BookingOperations.Refund
                );
                return Task.FromResult((Result<int>)r);
              }
            )
            // move the money back to user
            .DoAwait(
              DoType.MapErrors,
              b =>
                walletRepo
                  .BookEnd(b.Wallet.Id, b.Transaction.Record.Amount, 0)
                  .NullToError(b.Wallet.Id.ToString())
            )
            // Create transaction
            .DoAwait(
              DoType.MapErrors,
              b =>
                transactionRepo.Create(
                  b.Wallet.Id,
                  transactionGenerator.RefundBooking(b.Transaction.Record, b.Principal.Record)
                )
            )
            // return the priority fee: zinc failed to secure the ticket, so
            // the paid-for queue jump delivered nothing
            .DoAwait(DoType.MapErrors, this.RefundPriorityFeeIfAny)
            // update the booking
            .ThenAwait(x =>
              repo.Update(
                null,
                id,
                new BookingStatus { Status = BookStatus.Refunded, CompletedAt = DateTime.UtcNow },
                null,
                null
              )
            )
            // Error if Null
            .NullToError(id.ToString())
            // Re-retrieve full booking
            .ThenAwait(x => repo.Get(null, x.Id))
      )
      .NullToError(id.ToString())
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("refund"))
      .DoAwait(DoType.Ignore, notificationService.NotifyBookingRefunded)
      .Then(x => x.Principal, Errors.MapNone);
  }

  public Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    return repo.Delete(userId, id).DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"));
  }

  public Task<Result<IEnumerable<BookingCount>>> Count()
  {
    var singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore");
    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singapore);
    var dateNow = DateOnly.FromDateTime(now);
    var timeNow = TimeOnly.FromDateTime(now);

    logger.LogInformation("Get booking count after {Date} {Time}", dateNow, timeNow);

    return repo.Count(dateNow, timeNow, null, null);
  }

  public Task<Result<IEnumerable<BookingCount>>> Count(BookingCountSearch query)
  {
    var singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore");
    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singapore);
    var dateNow = DateOnly.FromDateTime(now);
    var timeNow = TimeOnly.FromDateTime(now);

    logger.LogInformation("Get booking count after {Date} {Time}", dateNow, timeNow);

    return repo.Count(dateNow, timeNow, query.Date, query.Direction);
  }

  // Returns the priority-fee snapshot to Usable with a ledger row. Runs inside
  // the caller's Refund/Cancel transaction, right after the booking-amount
  // refund, so fee and amount always move together. Terminated/Completed keep
  // the fee: the queue jump was consumed (a ticket was secured).
  private Task<Result<int>> RefundPriorityFeeIfAny(Booking b)
  {
    if (!b.Principal.Priority || b.Principal.PriorityFee is not > 0)
      return Task.FromResult((Result<int>)0);
    var fee = b.Principal.PriorityFee.Value;
    return walletRepo
      .Deposit(b.Wallet.Id, fee)
      .NullToError(b.Wallet.Id.ToString())
      .ThenAwait(_ =>
        transactionRepo.Create(
          b.Wallet.Id,
          transactionGenerator.RefundPriorityFee(fee, b.Principal.Record)
        )
      )
      .Then(_ => 0, Errors.MapNone);
  }

  // Eligibility against the unified policy chain: the first rule whose
  // conditions match decides access, fee (flat or % of the ticket) and the
  // timeslot's slot cap; no matching rule = deny.
  //
  // Targeting roles = the TARGET user's roles ∪ their admin-granted
  // ExtraRoles, the same union pricing uses (PurchasePricingRoles): when the
  // caller IS the target their JWT roles are authoritative
  // (callerTokenRoles != null); when checked on behalf of someone else (the
  // prioritize guard on an admin-assisted call) that user's JWT is absent, so
  // fall back to the persisted Descope-synced Roles mirror.
  public Task<Result<PriorityEligibility>> PriorityEligibility(
    string userId,
    string[]? callerTokenRoles = null
  )
  {
    // no booking in scope: hour-bounded rules are skipped, percent fees
    // cannot be computed (Fee = null) and slot caps cannot be counted
    return this.PriorityContext(userId, callerTokenRoles)
      .Then(
        t =>
        {
          var match = PriorityRules.Match(t.s.Policies, NowSgt(), null, userId, t.roles);
          if (match is not { Allow: true })
            return new PriorityEligibility
            {
              Eligible = false,
              Fee = null,
              Free = false,
              PolicyName = match?.Name,
            };
          var fee = PriorityRules.Fee(match, null);
          return new PriorityEligibility
          {
            Eligible = true,
            Fee = fee,
            Free = fee == 0m,
            SlotCap = match.SlotCap,
            PolicyName = match.Name,
          };
        },
        Errors.MapNone
      );
  }

  // booking-scoped eligibility: hour-bounded rules apply (hours until the
  // timeslot departs), percent fees compute off the booking's charged ticket
  // amount, and the matched rule's slot cap is counted; null when the booking
  // does not exist (or is not visible to userId)
  public Task<Result<PriorityEligibility?>> BookingPriorityEligibility(
    string? userId,
    Guid id,
    string[]? callerTokenRoles = null
  )
  {
    return repo.Get(userId, id)
      .ThenAwait(b =>
      {
        if (b == null)
          return Task.FromResult((Result<PriorityEligibility?>)(PriorityEligibility?)null);
        return this.PriorityContext(b.Principal.UserId, callerTokenRoles)
          .ThenAwait(t => this.EvaluateForBooking(t.s, t.roles, b))
          .Then(PriorityEligibility? (e) => e, Errors.MapNone);
      });
  }

  // settings + the pricing roles targeting matches on (JWT roles when the
  // caller is the target, else the persisted mirror)
  private Task<Result<(PrioritySettingsRecord s, string[] roles)>> PriorityContext(
    string userId,
    string[]? callerTokenRoles
  )
  {
    return prioritySettingsRepo
      .GetCurrent()
      .ThenAwait(s =>
      {
        // no rule targets anyone specific: skip the user lookup, roles are
        // never consulted
        if (s.Policies.All(p => p.Target == null))
          return Task.FromResult((Result<(PrioritySettingsRecord s, string[] roles)>)(s, []));
        return userRepo
          .GetById(userId)
          .Then(
            (PrioritySettingsRecord s, string[] roles) (u) =>
              (
                s,
                PurchasePricingRoles.For(
                  callerTokenRoles != null,
                  callerTokenRoles ?? [],
                  u?.Principal.Record
                )
              ),
            Errors.MapNone
          );
      });
  }

  private static TimeOnly NowSgt()
  {
    var singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore");
    return TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singapore));
  }

  // hours until the timeslot departs, in SGT (negative once departed)
  private static decimal HoursToDeparture(BookingRecord r)
  {
    var singapore = TimeZoneInfo.FindSystemTimeZoneById("Singapore");
    var nowSgt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singapore);
    return (decimal)(r.Date.ToDateTime(r.Time) - nowSgt).TotalHours;
  }

  // the single booking-scoped evaluation shared by the eligibility endpoint
  // and the prioritize guard: match the chain, price the fee off the
  // booking's charged ticket amount, count the matched rule's slot cap
  private Task<Result<PriorityEligibility>> EvaluateForBooking(
    PrioritySettingsRecord s,
    string[] roles,
    Booking b
  )
  {
    var rec = b.Principal.Record;
    var match = PriorityRules.Match(
      s.Policies,
      NowSgt(),
      HoursToDeparture(rec),
      b.Principal.UserId,
      roles
    );
    if (match is not { Allow: true })
      return Task.FromResult(
        (Result<PriorityEligibility>)new PriorityEligibility
        {
          Eligible = false,
          Fee = null,
          Free = false,
          PolicyName = match?.Name,
        }
      );

    var fee = PriorityRules.Fee(match, Math.Abs(b.Transaction.Record.Amount));
    if (match.SlotCap == null)
      return Task.FromResult(
        (Result<PriorityEligibility>)new PriorityEligibility
        {
          Eligible = true,
          Fee = fee,
          Free = fee == 0m,
          PolicyName = match.Name,
        }
      );

    return repo.CountSlotPriority(rec.Direction, rec.Date, rec.Time)
      .Then(
        c =>
        {
          var slotsLeft = Math.Max(0, match.SlotCap.Value - c);
          return new PriorityEligibility
          {
            // a full priority queue blocks everyone, however eligible
            Eligible = slotsLeft > 0,
            Fee = fee,
            Free = fee == 0m,
            SlotCap = match.SlotCap,
            SlotsLeft = slotsLeft,
            PolicyName = match.Name,
          };
        },
        Errors.MapNone
      );
  }

  // Moves a booking to the front of its timeslot's purchase queue. Guarded
  // (Pending only, not already priority, owner eligible) and wrapped in ONE
  // RepeatableRead transaction so the guard read, the fee collection from
  // Usable, the ledger row and the priority flag can never diverge — an
  // insufficient balance rolls everything back and surfaces as its own error.
  public Task<Result<BookingPrincipal?>> Prioritize(
    string? userId,
    Guid id,
    string? callerSub = null
  )
  {
    return transaction
      .Start(
        () =>
          repo.Get(userId, id)
            .NullToError(id.ToString())
            // guard: only a Pending, not-yet-priority booking may jump
            .DoAwait(
              DoType.MapErrors,
              b =>
              {
                if (b.Principal.Status.Status == BookStatus.Pending && !b.Principal.Priority)
                  return Task.FromResult((Result<int>)0);
                var r = new InvalidBookingOperationException(
                  b.Principal.Priority
                    ? "Prioritize requires a booking that is not already priority"
                    : "Prioritize requires a booking in 'Pending' Status",
                  b.Principal.Status.Status,
                  BookingOperations.Prioritize
                );
                return Task.FromResult((Result<int>)r);
              }
            )
            // guard: the booking's owner must be eligible right now — the
            // full booking-scoped evaluation: policy chain (with hours to
            // this timeslot's departure), free/access targeting on the
            // owner's pricing roles (their JWT is absent here, so the
            // persisted Roles mirror is used) and the per-timeslot slot cap
            .ThenAwait(b =>
              this.PriorityContext(b.Principal.UserId, null)
                .ThenAwait(t => this.EvaluateForBooking(t.s, t.roles, b))
                .ThenAwait(e =>
                {
                  if (e.Eligible)
                    return Task.FromResult((Result<(Booking b, PriorityEligibility e)>)(b, e));
                  var r = new InvalidBookingOperationException(
                    e.SlotsLeft == 0
                      ? "Prioritize requires a free priority slot — this timeslot's priority queue is full"
                      : "Prioritize requires a priority policy that allows the booking's owner right now",
                    b.Principal.Status.Status,
                    BookingOperations.Prioritize
                  );
                  return Task.FromResult((Result<(Booking b, PriorityEligibility e)>)r);
                })
            )
            // collect the fee from Usable + ledger row (a zero or free fee
            // books neither — a "SGD 0.00 charged" row would contradict the
            // fee being disabled/waived). On the booking-scoped path an
            // eligible answer always carries a concrete fee.
            .DoAwait(
              DoType.MapErrors,
              t =>
                t.e.Fee > 0
                  ? walletRepo
                    .Collect(t.b.Wallet.Id, t.e.Fee.Value)
                    .NullToError(t.b.Wallet.Id.ToString())
                    .ThenAwait(_ =>
                      transactionRepo.Create(
                        t.b.Wallet.Id,
                        transactionGenerator.PriorityFeeCharge(t.e.Fee.Value, t.b.Principal.Record)
                      )
                    )
                    .Then(_ => 0, Errors.MapNone)
                  : Task.FromResult((Result<int>)0)
            )
            // flag the booking + snapshot the charged fee for the refund
            // path; a FREE boost snapshots null (nothing was charged, so
            // there is nothing to refund — the refund path must stay silent).
            // grantedBy = the caller's sub only when they are NOT the owner
            // (an admin boosting someone else's booking) — boost-ledger
            // attribution; a self-boost (free or paid) stays unattributed
            .ThenAwait(t =>
              repo.Prioritize(
                userId,
                id,
                t.e.Free ? null : t.e.Fee,
                callerSub != null && callerSub != t.b.Principal.UserId ? callerSub : null
              )
            )
            .NullToError(id.ToString())
      )
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("update"))
      .Then(BookingPrincipal? (x) => x, Errors.MapNone);
  }
}
