using App.Error.V1;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Payment;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Payments.Data;

public class PaymentRepository(MainDbContext db, ILogger<PaymentRepository> logger) : IPaymentRepository
{
  public async Task<Result<IEnumerable<PaymentPrincipal>>> Search(PaymentSearch search)
  {
    try
    {
      logger.LogInformation("Searching for Payments with '{@Search}'", search.ToJson());

      var query = db.Payments.AsQueryable();
      if (!string.IsNullOrWhiteSpace(search.Reference))
        query = query.Where(x => EF.Functions.ILike(x.ExternalReference, $"%{search.Reference}%"));
      if (search.Id is not null) query = query.Where(x => search.Id == x.Id);
      if (search.WalletId is not null) query = query.Where(x => x.WalletId == search.WalletId);
      if (search.TransactionId is not null)
        query = query
          .Include(x => x.Transaction)
          .Where(x => x.Transaction != null && x.Transaction.Id == search.TransactionId);

      if (search.Gateway is not null)
        query = query.Where(x => x.Gateway == search.Gateway);

      if (search.MaxAmount is not null)
        query = query.Where(x => x.Amount <= search.MaxAmount);

      if (search.MinAmount is not null)
        query = query.Where(x => x.Amount >= search.MinAmount);

      if (search.CreatedBefore is not null)
        query = query.Where(x => x.CreatedAt <= search.CreatedBefore);

      if (search.CreatedAfter is not null)
        query = query.Where(x => x.CreatedAt >= search.CreatedAfter);

      if (search.LastUpdatedBefore is not null)
        query = query.Where(x => x.LastUpdated <= search.LastUpdatedBefore);

      if (search.LastUpdatedAfter is not null)
        query = query.Where(x => x.LastUpdated >= search.LastUpdatedAfter);

      if (search.Status is not null)
        query = query.Where(x => x.Status == search.Status);

      var result = await query
        .OrderByDescending(x => x.LastUpdated)
        .Skip(search.Skip)
        .Take(search.Limit)
        .ToArrayAsync();

      return result
        .Select(x => x.ToPrincipal())
        .ToResult();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed search for Payments with '{@Search}'", search.ToJson());
      throw;
    }
  }

  public async Task<Result<Payment?>> GetById(Guid id)
  {
    try
    {
      logger.LogInformation("Retrieving Payment '{Id}'", id);
      var payment = await db
        .Payments
        .Include(x => x.Wallet)
        .Include(x => x.Transaction)
        .Where(x => x.Id == id)
        .FirstOrDefaultAsync();
      return payment?.ToDomain();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed retrieving Payment '{Id}'", id);
      throw;
    }
  }

  public async Task<Result<Payment?>> GetByRef(string id)
  {
    try
    {
      logger.LogInformation("Retrieving Payment via '{Reference}'", id);
      var payment = await db
        .Payments
        .Include(x => x.Wallet)
        .Include(x => x.Transaction)
        .Where(x => x.ExternalReference == id)
        .FirstOrDefaultAsync();
      return payment?.ToDomain();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed retrieving Payment via '{Reference}'", id);
      throw;
    }
  }

  public async Task<Result<PaymentPrincipal>> Create(Guid walletId, PaymentReference reference, PaymentRecord record)
  {
    try
    {
      logger.LogInformation("Creating Payment: {@Reference} with record '{@Record}' in Wallet '{WalletId}",
        reference.ToJson(), record.ToJson(), walletId);

      var now = DateTime.UtcNow;
      var data = reference.ToData();
      data.WalletId = walletId;
      data = data.UpdateData(record);
      data.CreatedAt = now;

      var kvp = new PaymentStatusEntryData
      {
        Status = "created",
        Updated = now,
      };

      data.Statuses = new PaymentStatusData { Statuses = [kvp] };

      var r = db.Payments.Add(data);
      await db.SaveChangesAsync();

      return r.Entity.ToPrincipal();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed creating payment under wallet '{WalletId}' with ref '{@Reference}' and record {@Record} ",
          walletId,
          reference.ToJson(), record.ToJson());
      throw;
    }
  }

  public async Task<Result<Payment?>> UpdateById(Guid id, PaymentRecord v2)
  {
    try
    {
      logger.LogInformation("Updating Payment '{Id} (ID)' with: {@Record}",
        id, v2.ToJson());

      var v1 = await db.Payments
        .Where(x => x.Id == id)
        .Include(x => x.Transaction)
        .Include(x => x.Wallet)
        .FirstOrDefaultAsync();

      if (v1 == null) return (Payment?)null;

      v1 = v1.UpdateData(v2);
      v1.Statuses.Statuses.Add(new PaymentStatusEntryData { Status = v2.Status, Updated = v2.LastUpdated });

      var updated = db.Payments.Update(v1);
      await db.SaveChangesAsync();
      return updated.Entity.ToDomain();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to update Payment '{Id} (ID)' with: {@Record} due to conflict with existing record",
        id, v2.ToJson());
      return new EntityConflict(
          $"Failed to update Payment '{id} (ID)' due to conflicting with existing record",
          typeof(Payment))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to update Payment '{Id} (ID)' under with: '{@Record}'",
        id, v2.ToJson());
      throw;
    }
  }

  public async Task<Result<Payment?>> UpdateByRef(string reference, PaymentRecord v2)
  {
    try
    {
      logger.LogInformation("Updating Payment '{Reference} (Reference)' with: {@Record}",
        reference, v2.ToJson());

      var v1 = await db.Payments
        .Where(x => x.ExternalReference == reference)
        .Include(x => x.Transaction)
        .Include(x => x.Wallet)
        .FirstOrDefaultAsync();

      if (v1 == null) return (Payment?)null;

      v1 = v1.UpdateData(v2);
      v1.Statuses.Statuses.Add(new PaymentStatusEntryData { Status = v2.Status, Updated = v2.LastUpdated });

      var updated = db.Payments.Update(v1);
      await db.SaveChangesAsync();
      return updated.Entity.ToDomain();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to update Payment '{Reference} (Reference)' with: {@Record} due to conflict with existing record",
        reference, v2.ToJson());
      return new EntityConflict(
          $"Failed to update Payment '{reference} (Reference)' due to conflicting with existing record",
          typeof(Payment))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to update Payment '{Reference} (Reference)' under with: '{@Record}'",
        reference, v2.ToJson());
      throw;
    }
  }

  public async Task<Result<Unit?>> Link(Guid transactionId, Guid paymentId)
  {
    try
    {
      logger.LogInformation("Linking Transaction '{transactionId}' to Payment '{paymentId}'",
        transactionId, paymentId);

      var v1 = await db.Transactions
        .Where(x => x.Id == transactionId)
        .FirstOrDefaultAsync();

      if (v1 == null) return (Unit?)null;

      v1.PaymentId = paymentId;

      db.Transactions.Update(v1);
      await db.SaveChangesAsync();

      return new Result<Unit?>();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to link Transaction '{transactionId}' to Payment '{paymentId}'",
        transactionId, paymentId);
      throw;
    }
  }

  public async Task<Result<Unit?>> DeleteById(Guid id)
  {
    try
    {
      logger.LogInformation("Deleting Payment '{Id} (ID)'", id);
      var a = await db.Payments
        .Where(x => x.Id == id)
        .FirstOrDefaultAsync();
      if (a == null) return (Unit?)null;

      db.Payments.Remove(a);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Payment with ID '{Id}", id);
      throw;
    }
  }

  public async Task<Result<Unit?>> DeleteByRef(string reference)
  {
    try
    {
      logger.LogInformation("Deleting Payment '{Reference} (Reference)'", reference);
      var a = await db.Payments
        .Where(x => x.ExternalReference == reference)
        .FirstOrDefaultAsync();
      if (a == null) return (Unit?)null;

      db.Payments.Remove(a);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Payment with Reference '{Reference}", reference);
      throw;
    }
  }
}
