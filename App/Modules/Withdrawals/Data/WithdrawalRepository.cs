using App.Error.V1;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain;
using Domain.Wallet;
using Domain.Withdrawal;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Withdrawals.Data;

public class WithdrawalRepository(MainDbContext db, ILogger<WithdrawalRepository> logger) : IWithdrawalRepository
{
  public async Task<Result<IEnumerable<WithdrawalPrincipal>>> Search(WithdrawalSearch search)
  {
    try
    {
      var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");
      logger.LogInformation("Searching for Withdrawal with '{@Search}'", search.ToJson());
      var query = db.Withdrawals.AsQueryable();
      if (!string.IsNullOrWhiteSpace(search.UserId))
        query = query
          .Include(x => x.Wallet)
          .Where(x => search.UserId == x.Wallet.UserId);
      if (search.CompleterId is not null)
        query = query.Where(x => x.CompleterId == search.CompleterId);
      if (search.Id is not null)
        query = query.Where(x => search.Id == x.Id);

      if (search.Min is not null)
        query = query.Where(x => x.Amount >= search.Min);
      if (search.Max is not null)
        query = query.Where(x => x.Amount <= search.Max);

      if (search.Status is not null)
        query = query.Where(x => x.Status == (byte)search.Status);

      if (search.Before != null)
      {
        var dt = search.Before?.ToZonedDateTime(TimeOnly.MaxValue, tz);
        query = query.Where(x => x.CreatedAt <= dt);
      }

      if (search.After != null)
      {
        var dt = search.After?.ToZonedDateTime(TimeOnly.MinValue, tz);
        query = query.Where(x => x.CreatedAt >= dt);
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
      logger.LogError(e, "Failed search for Withdrawal with {@Search}", search.ToJson());
      throw;
    }
  }

  public async Task<Result<Withdrawal?>> Get(Guid id, string? userId)
  {
    try
    {
      logger.LogInformation("Get for withdrawal with {id} by optional user {userId}", id, userId);
      var wallet = await db
        .Withdrawals
        .Include(x => x.Wallet)
        .ThenInclude(x => x.User)
        .Include(x => x.Completer)
        .Where(x => x.Id == id && (userId == null || userId == x.Wallet.UserId))
        .FirstOrDefaultAsync();
      return wallet?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed Get for Withdrawal with {id} by optional user {userId}", id, userId);
      throw;
    }
  }

  public async Task<Result<WithdrawalPrincipal>> Create(Guid walletId, WithdrawalRecord record)
  {
    try
    {
      logger.LogInformation("Creating Withdrawal {@Record} from wallet '{walletId}'", record.ToJson(), walletId);
      var data = new WithdrawalData
      {
        CreatedAt = DateTime.UtcNow,

        // record
        Status = (byte)WithdrawStatus.Pending,
        CompletedAt = null,
        Note = null,
        Receipt = null,
        WalletId = walletId,
      };

      data = data.Update(record);

      var r = db.Withdrawals.Add(data);
      await db.SaveChangesAsync();

      return r.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to create Withdrawal under Wallet '{WalletId}': {@Record} due to conflict with existing record",
        walletId, record.ToJson());
      return new EntityConflict(
          $"Failed to create Withdrawal under Wallet '{walletId}' due to conflicting with existing record",
          typeof(WalletPrincipal))
        .ToException();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed creating withdrawal with wallet '{WalletId}' and record {@Record} ", walletId,
          record.ToJson());
      throw;
    }
  }

  public async Task<Result<WithdrawalPrincipal?>> Update(string? userId, Guid id, WithdrawalRecord? record,
    WithdrawalStatus? status, WithdrawalComplete? complete)
  {
    try
    {
      logger.LogInformation(
        "Updating Withdrawal '{Id}' under User '{UserId}' with: {@Record}, {@Status} and {@Complete}",
        id, userId ?? "null",
        record?.ToJson() ?? "null", status?.ToJson() ?? "null", complete?.ToJson() ?? "null");
      var v1 = await db.Withdrawals
        .Include(x => x.Wallet)
        .Where(x =>
          x.Id == id
          &&
          (userId == null || x.Wallet.UserId == userId)
        )
        .FirstOrDefaultAsync();

      if (v1 == null) return (WithdrawalPrincipal?)null;

      if (record is not null) v1 = v1.Update(record);
      if (status is not null) v1 = v1.Update(status);
      if (complete is not null) v1 = v1.Update(complete);

      var updated = db.Withdrawals.Update(v1);
      await db.SaveChangesAsync();
      return updated.Entity.ToPrincipal();
    }

    catch (Exception e)
    {
      logger.LogError(e,
        "Failed to updating Withdrawal '{Id}' from User {UserId} '{@Record}, {@Status} and {@Complete}'",
        id, userId, record?.ToJson() ?? "null", status?.ToJson() ?? "null", complete?.ToJson() ?? "null");
      return e;
    }
  }

  public async Task<Result<Unit?>> Delete(Guid id)
  {
    try
    {
      logger.LogInformation("Deleting Withdrawal '{Id}'", id);
      var a = await db
        .Withdrawals
        .Where(x => x.Id == id)
        .FirstOrDefaultAsync();
      if (a == null) return (Unit?)null;

      db.Withdrawals.Remove(a);
      await db.SaveChangesAsync();

      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Withdrawal '{Id}'", id);
      return e;
    }
  }
}
