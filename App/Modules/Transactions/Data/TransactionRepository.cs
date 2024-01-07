using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain;
using Domain.Transaction;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Transactions.Data;

public class TransactionRepository(MainDbContext db, ILogger<TransactionRepository> logger) : ITransactionRepository
{
  public async Task<Result<IEnumerable<TransactionPrincipal>>> Search(TransactionSearch search)
  {
    try
    {
      var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
      logger.LogInformation("Searching for Transaction with '{@Search}'", search.ToJson());

      var query = db.Transactions.AsQueryable();
      if (!string.IsNullOrWhiteSpace(search.Search))
        query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search.Search}%"));
      if (search.Id is not null) query = query.Where(x => search.Id == x.Id);
      if (search.TransactionType is not null)
        query = query.Where(x => (int)search.TransactionType == x.TransactionType);
      if (!string.IsNullOrWhiteSpace(search.userId))
        query = query.Include(x => x.Wallet)
          .Where(x => x.Wallet.UserId == search.userId);
      if (search.WalletId is not null)
        query = query.Where(x => x.WalletId == search.WalletId);
      if (search.Before is not null)
      {
        var b = search.Before?.ToZonedDateTime(TimeOnly.MaxValue, tz);
        query = query.Where(x => x.CreatedAt <= b);
      }

      if (search.After is not null)
      {
        var a = search.After?.ToZonedDateTime(TimeOnly.MinValue, tz);
        query = query.Where(x => x.CreatedAt >= a);
      }

      var result = await query
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
        .LogError(e, "Failed search for Wallet with {@Search}", search.ToJson());
      throw;
    }
  }

  public async Task<Result<Transaction?>> Get(Guid id, string? userId)
  {
    try
    {
      logger.LogInformation("Retrieving Transaction '{Id}' with optional owner '{userId}'", id, userId ?? "null");
      var wallet = await db
        .Transactions
        .Include(x => x.Wallet)
        .Where(x => x.Id == id && (userId == null || x.Wallet.UserId == userId))
        .FirstOrDefaultAsync();
      return wallet?.ToDomain();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed retrieving Wallet with Id: {Id}", id);
      throw;
    }
  }

  public async Task<Result<TransactionPrincipal>> Create(Guid walletId, TransactionRecord record)
  {
    try
    {
      logger.LogInformation("Creating Transaction: {@Record} in Wallet '{WalletId}", record.ToJson(), walletId);
      var data = new TransactionData { CreatedAt = DateTime.UtcNow, WalletId = walletId, };

      data = data.Update(record);

      var r = db.Transactions.Add(data);
      await db.SaveChangesAsync();

      return r.Entity.ToPrincipal();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed creating transaction with wallet '{WalletId}' and record {@Record} ", walletId,
          record.ToJson());
      throw;
    }
  }

  public async Task<Result<Unit?>> Delete(Guid id)
  {
    try
    {
      logger.LogInformation("Deleting Transaction '{Id}'", id);
      var a = await db.Transactions
        .Where(x => x.Id == id)
        .FirstOrDefaultAsync();
      if (a == null) return (Unit?)null;

      db.Transactions.Remove(a);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Transaction record with ID '{Id}", id);
      throw;
    }
  }
}
