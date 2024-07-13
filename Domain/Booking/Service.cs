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
  ILogger<BookingService> logger)
  : IBookingService
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
    return transaction.Start(() =>
      walletRepo.GetByUserId(userId)
        .NullToError(userId)
        .ThenAwait(x => walletRepo.BookStart(x.Principal.Id, cost))
        .NullToError(userId)
        .ThenAwait(w => transactionRepo.Create(w.Id, transactionGenerator.CreateBooking(cost, record)))
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
  public Task<Result<BookingPrincipal?>> Reserve(TrainDirection direction, DateOnly date, TimeOnly time)
  {
    return repo.Reserve(direction, date, time);
  }

  // This marks the ticket in the buying status
  public Task<Result<BookingPrincipal?>> Buying(Guid id)
  {
    return repo.Update(null, id, new BookingStatus() { Status = BookStatus.Buying, CompletedAt = null }, null, null)
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"));
  }

  public Task<Result<BookingPrincipal?>> RevertBuying(Guid id)
  {
    return repo.Update(null, id, new BookingStatus() { Status = BookStatus.Pending, CompletedAt = null }, null, null)
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("create"));
  }

  // This marks the ticket in the bought status, need to move $$
  public Task<Result<BookingPrincipal?>> Complete(Guid id, string bookingNo, string ticketNo, Stream file)
  {
    return fileRepo.Save(file)
      .ThenAwait(fileId =>
        transaction.Start(() =>
          // get booking
          repo
            .Get(null, id)
            // error if null
            .NullToError(id.ToString())
            // move the money
            .DoAwait(DoType.MapErrors, b => walletRepo
              .BookEnd(b.Wallet.Id, 0, b.Transaction.Record.Amount)
              .NullToError(b.Wallet.Id.ToString())
            )
            // Create transaction from original transaction and booking
            .ThenAwait(b =>
              transactionRepo.Create(b.Wallet.Id,
                transactionGenerator.CompleteBooking(b.Transaction.Record, b.Principal.Record))
            )
            // Update booking
            .ThenAwait(x => repo.Update(null, id,
              new BookingStatus { Status = BookStatus.Completed, CompletedAt = DateTime.UtcNow },
              null,
              new BookingComplete { Ticket = fileId, BookingNumber = bookingNo, TicketNumber = ticketNo, })
            )
        )
      )
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"));
  }

  // When user cancels the tickets before booking succeeded
  public Task<Result<BookingPrincipal?>> Cancel(string? userId, Guid id)
  {
    return transaction.Start(() =>
      repo
        // get booking
        .Get(userId, id)
        // error if null
        .NullToError(id.ToString())
        // block cancelling if status is NOT pending
        .DoAwait(DoType.MapErrors, b =>
        {
          if (b.Principal.Status.Status == BookStatus.Pending) return Task.FromResult((Result<int>)0);
          var r = new InvalidBookingOperationException("Cancellation require booking to be in 'Pending' Status",
            b.Principal.Status.Status, BookingOperations.Cancel);
          return Task.FromResult((Result<int>)r);
        })
        // move the money
        .DoAwait(DoType.MapErrors, b => walletRepo
          .BookEnd(b.Wallet.Id, b.Transaction.Record.Amount, 0)
          .NullToError(b.Wallet.Id.ToString())
        )
        // Create transaction 
        .DoAwait(DoType.MapErrors, b =>
          transactionRepo.Create(b.Wallet.Id,
            transactionGenerator.CancelBooking(b.Transaction.Record, b.Principal.Record))
        )
        // update the booking
        .ThenAwait(x => repo.Update(
          userId, id,
          new BookingStatus { Status = BookStatus.Cancelled, CompletedAt = DateTime.UtcNow },
          null, null)
        )
    )
    .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"));
  }

  // When users cancel the tickets after booking succeeded
  public Task<Result<BookingPrincipal>> Terminate(string? userId, Guid id, DateTime referenceTime)
  {
    return transaction.Start(() =>
      repo
        // get booking
        .Get(userId, id)
        // error if null
        .NullToError(id.ToString())
        // block terminating if status is NOT complete
        .DoAwait(DoType.MapErrors, b =>
        {
          if (b.Principal.Status.Status == BookStatus.Completed) return Task.FromResult((Result<int>)0);
          var r = new InvalidBookingOperationException("Termination require booking to be in 'Completed' Status",
            b.Principal.Status.Status, BookingOperations.Terminate);
          return Task.FromResult((Result<int>)r);
        })
        // block terminating if ticket is 30 min before departure
        .DoAwait(DoType.MapErrors, b =>
        {
          var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
          var t = b.Principal.Record.Date.ToZonedDateTime(b.Principal.Record.Time, tz);
          if (referenceTime < t) return Task.FromResult((Result<int>)0);
          var r = new InvalidBookingOperationException($"Cannot terminate booking past buffer time before departure",
            b.Principal.Status.Status, BookingOperations.Terminate);
          return Task.FromResult((Result<int>)r);
        })
        // move the money
        .DoAwait(DoType.MapErrors, b => walletRepo
          .Deposit(b.Wallet.Id, b.Transaction.Record.Amount * calculator.RefundRate)
          .NullToError(b.Wallet.Id.ToString())
        )
        // Create transaction 
        .DoAwait(DoType.MapErrors, b =>
          transactionRepo.Create(b.Wallet.Id,
            transactionGenerator.TerminateBooking(b.Transaction.Record, b.Principal.Record))
        )

        // update the booking
        .ThenAwait(x => repo.Update(
          userId, id,
          new BookingStatus { Status = BookStatus.Terminated, CompletedAt = DateTime.UtcNow },
          null, null)
        )
    )
    .NullToError(id.ToString())
    // terminate the booking in KTMB through tin
    .DoAwait(DoType.Ignore, b => terminatorRepository.Terminate(
      new BookingTermination(b.Complete.BookingNumber!, b.Complete.TicketNumber!))
    )
    .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"));
  }

  // When system cancels the tickets after failed
  public Task<Result<BookingPrincipal?>> Refund(Guid id)
  {
    return transaction.Start(() =>
      repo
        // get booking
        .Get(null, id)
        // error if null
        .NullToError(id.ToString())
        // block terminating if status is NOT complete
        .DoAwait(DoType.MapErrors, b =>
        {
          if (b.Principal.Status.Status == BookStatus.Pending) return Task.FromResult((Result<int>)0);
          var r = new InvalidBookingOperationException("Refund require booking to be in 'Pending' Status",
            b.Principal.Status.Status, BookingOperations.Refund);
          return Task.FromResult((Result<int>)r);
        })
        // move the money back to user
        .DoAwait(DoType.MapErrors, b => walletRepo
          .BookEnd(b.Wallet.Id, b.Transaction.Record.Amount, 0)
          .NullToError(b.Wallet.Id.ToString())
        )
        // Create transaction 
        .DoAwait(DoType.MapErrors, b =>
          transactionRepo.Create(b.Wallet.Id,
            transactionGenerator.RefundBooking(b.Transaction.Record, b.Principal.Record))
        )
        // update the booking
        .ThenAwait(x => repo.Update(
          null, id,
          new BookingStatus { Status = BookStatus.Refunded, CompletedAt = DateTime.UtcNow },
          null, null)
        )
    )
    .DoAwait(DoType.Ignore, _ => cdcRepository.Add("refund"));
  }

  public Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    return repo.Delete(userId, id)
      .DoAwait(DoType.Ignore, _ => cdcRepository.Add("reserve"));
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
