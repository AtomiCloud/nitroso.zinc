using CSharp_Result;
using Domain.Exceptions;
using Domain.Timings;
using Domain.Transaction;
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
  IRefundCalculator calculator,
  IBookingTerminatorRepository terminatorRepository,
  IBookingCdcRepository cdcRepository,
  IBookingNotificationService notificationService,
  ILogger<BookingService> logger
) : IBookingService
{
  public Task<Result<IEnumerable<BookingPrincipal>>> Search(BookingSearch search)
  {
    return repo.Search(search);
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
  public Task<Result<BookingPrincipal>> Create(string userId, decimal cost, BookingRecord record)
  {
    return transaction
      .Start(
        () =>
          walletRepo
            .GetByUserId(userId)
            .NullToError(userId)
            .ThenAwait(x => walletRepo.BookStart(x.Principal.Id, cost))
            .NullToError(userId)
            .ThenAwait(w =>
              transactionRepo.Create(w.Id, transactionGenerator.CreateBooking(cost, record))
            )
            .ThenAwait(x => repo.Create(userId, x.Id, record))
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
  public Task<Result<BookingPrincipal?>> Revert(Guid id)
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
                b.Principal.Status.Status == BookStatus.Buying
                && b.Principal.Complete.BookingNumber == null
              )
                return Task.FromResult((Result<int>)0);
              var r = new InvalidBookingOperationException(
                "Revert requires an uncaptured booking in 'Buying' Status",
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
                    && b.Principal.Complete.BookingNumber == null
                  )
                    return Task.FromResult((Result<int>)0);
                  var r = new InvalidBookingOperationException(
                    "Completion requires an uncompleted booking in 'Pending', 'Buying' or 'Recovering' Status",
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
      .DoAwait(DoType.Ignore, x =>notificationService.NotifyBookingCompleted(x)
          .Match(s => s, 
            exception =>
            {
              logger.LogError(exception, "Failed to notify booking completed");
              return new Unit();
            }), Errors.MapNone)
      .Then(BookingPrincipal? (x) => x.Principal , Errors.MapNone);
      
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
                if (b.Principal.Status.Status == BookStatus.Pending)
                  return Task.FromResult((Result<int>)0);
                var r = new InvalidBookingOperationException(
                  "Cancellation require booking to be in 'Pending' Status",
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
      .DoAwait(DoType.Ignore, x =>notificationService.NotifyBookingCancelled(x)
        .Match(s =>
          {
            logger.LogInformation("Notify booking cancelled successfully");
            return s;
          }, 
          exception =>
          {
            logger.LogError(exception, "Failed to notify booking completed");
            return new Unit();
          }), Errors.MapNone)
      .Then(BookingPrincipal? (x) => x.Principal , Errors.MapNone);
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
      .DoAwait(DoType.Ignore, x =>notificationService.NotifyBookingDuplicate(x)
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
      .Then(BookingPrincipal? (x) => x.Principal , Errors.MapNone);
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
            // move the money
            .DoAwait(
              DoType.MapErrors,
              b =>
                walletRepo
                  .Deposit(b.Wallet.Id, b.Transaction.Record.Amount * calculator.RefundRate)
                  .NullToError(b.Wallet.Id.ToString())
            )
            // Create transaction
            .DoAwait(
              DoType.MapErrors,
              b =>
                transactionRepo.Create(
                  b.Wallet.Id,
                  transactionGenerator.TerminateBooking(b.Transaction.Record, b.Principal.Record)
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
}
