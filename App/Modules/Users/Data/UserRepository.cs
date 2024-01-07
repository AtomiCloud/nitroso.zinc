using System.Transactions;
using App.Error.V1;
using App.Modules.Wallets.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.User;
using Domain.Wallet;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Users.Data;

public class UserRepository(MainDbContext db, ILogger<UserRepository> logger) : IUserRepository
{
  public async Task<Result<IEnumerable<UserPrincipal>>> Search(UserSearch search)
  {
    try
    {
      logger.LogInformation("Searching for User with '{@Search}'", search);

      var query = db.Users.AsQueryable();
      if (!string.IsNullOrWhiteSpace(search.Username))
        query = query.Where(x => EF.Functions.ILike(x.Username, $"%{search.Username}%"));
      if (!string.IsNullOrWhiteSpace(search.Id))
        query = query.Where(x => EF.Functions.ILike(x.Id.ToString(), $"%{search.Id}%"));

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
        .LogError(e, "Failed search for User with {@Search}", search);
      return e;
    }
  }

  public async Task<Result<User?>> GetById(string id)
  {
    try
    {
      logger.LogInformation("Retrieving User with Id '{Id}'", id);
      var user = await db
        .Users
        .Include(x => x.Wallet)
        .Where(x => x.Id == id)
        .FirstOrDefaultAsync();
      return user?.ToDomain();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed retrieving User with Id: {Id}", id);
      throw;
    }
  }

  public async Task<Result<User?>> GetByUsername(string username)
  {
    try
    {
      logger.LogInformation("Retrieving User by Username: {Username}", username);
      var user = await db.Users
        .Include(x => x.Wallet)
        .Where(x => x.Username == username)
        .FirstOrDefaultAsync();
      return user?.ToDomain();
    }
    catch (Exception e)
    {
      logger
        .LogError(e, "Failed retrieving User by Username: {Username}", username);
      throw;
    }
  }

  public async Task<Result<bool>> Exists(string username)
  {
    try
    {
      return await db.Users.AnyAsync(x => x.Username == username);
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to check username exist {Username}", username);
      throw;
    }
  }

  public async Task<Result<UserPrincipal>> Create(string id, UserRecord record)
  {
    try
    {
      using var scope = new TransactionScope(
        TransactionScopeOption.Required,
        new TransactionOptions
        {
          IsolationLevel = IsolationLevel.RepeatableRead
        }, TransactionScopeAsyncFlowOption.Enabled);

      // Creating user
      logger.LogInformation("Creating User: {@Record}", record.ToJson());
      var data = record.ToData();
      data.Id = id;
      var r = db.Users.Add(data);
      db.SaveChanges();

      // if (id != "water") throw new Exception();
      // Create Wallet, based on user
      logger.LogInformation("Creating Wallet for user {UserId}", id);
      var walletData = new WalletData
      {
        Usable = 0,
        WithdrawReserve = 0,
        BookingReserve = 0,
        UserId = id,
      };
      db.Wallets.Add(walletData);
      await db.SaveChangesAsync();


      scope.Complete();
      return r.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to create User due to conflicting with existing record for JWT sub '{Sub}': {@Record}", id,
        record.ToJson());

      return new EntityConflict("Failed to create User due to conflicting with existing record", typeof(UserPrincipal))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to create User for JWT sub '{Sub}': {@Record}", id, record.ToJson());
      throw;
    }
  }

  public async Task<Result<UserPrincipal?>> Update(string id, UserRecord v2)
  {
    try
    {
      logger.LogInformation("Updating User '{Id}' with: {@Record}", id, v2.ToJson());
      var v1 = await db.Users
        .Where(x => x.Id == id)
        .FirstOrDefaultAsync();
      if (v1 == null) return (UserPrincipal?)null;

      var v3 = v1.Update(v2);

      var updated = db.Users.Update(v3);
      await db.SaveChangesAsync();
      return updated.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(e,
        "Failed to update User due to conflicting with existing record for JWT sub '{Sub}': {@Record}", id,
        v2.ToJson());
      return new EntityConflict("Failed to update User due to conflicting with existing record", typeof(UserPrincipal))
        .ToException();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to update User for JWT sub '{Sub}': {@Record}", id, v2.ToJson());
      throw;
    }
  }

  public async Task<Result<Unit?>> Delete(string id)
  {
    try
    {
      logger.LogInformation("Deleting User '{Id}'", id);
      var a = await db.Users
        .Where(x => x.Id == id)
        .FirstOrDefaultAsync();
      if (a == null) return (Unit?)null;

      db.Users.Remove(a);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete User record with ID '{Id}", id);
      throw;
    }
  }
}
