using App.Error.V1;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Transaction;
using Domain.Wallet;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Wallets.Data;

public class WalletRepository(MainDbContext db, ILogger<WalletRepository> logger)
  : IWalletRepository
{
  public async Task<Result<IEnumerable<WalletPrincipal>>> Search(WalletSearch search)
  {
    try
    {
      logger.LogInformation("Searching for Wallet with '{@Search}'", search.ToJson());

      var query = db.Wallets.AsQueryable();
      if (!string.IsNullOrWhiteSpace(search.UserId))
        query = query.Where(x => search.UserId == x.UserId);
      if (search.Id is not null)
        query = query.Where(x => search.Id == x.Id);
      var result = await query.Skip(search.Skip).Take(search.Limit).ToArrayAsync();

      return result.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed search for Wallet with {@Search}", search.ToJson());
      throw;
    }
  }

  public async Task<Result<Wallet?>> Get(Guid id, string? userId)
  {
    try
    {
      logger.LogInformation(
        "Retrieving Wallet with Id '{Id}' under optional user '{UserId}'",
        id,
        userId
      );
      var wallet = await db
        .Wallets.Include(x => x.User)
        .Where(x => x.Id == id && (userId == null || userId == x.UserId))
        .FirstOrDefaultAsync();
      return wallet?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed retrieving Wallet with Id: {Id}", id);
      throw;
    }
  }

  public async Task<Result<Wallet?>> GetByUserId(string userId)
  {
    try
    {
      logger.LogInformation("Retrieving Wallet with UserId '{UserId}'", userId);
      var wallet = await db
        .Wallets.Include(x => x.User)
        .Where(x => x.UserId == userId)
        .FirstOrDefaultAsync();
      return wallet?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed retrieving Wallet with UserId: {UserId}", userId);
      throw;
    }
  }

  public async Task<Result<WalletPrincipal?>> PrepareWithdraw(Guid id, decimal amount)
  {
    try
    {
      logger.LogInformation("Preparing withdrawal wallet with Id '{id}' with {amount}", id, amount);
      var wallet = await db.Wallets.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (wallet is null)
        return wallet?.ToPrincipal();

      if (wallet.Usable < amount)
        return new InsufficientBalance(
          "Insufficient balance to reserve withdraw",
          wallet.UserId,
          wallet.Id,
          amount,
          Accounts.Usable.Id
        ).ToException();

      wallet.Usable -= amount;
      wallet.WithdrawReserve += amount;
      await db.SaveChangesAsync();
      return wallet.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to reserve for withdrawal in wallet with Id: {id} with {amount}",
        id,
        amount
      );
      throw;
    }
  }

  public async Task<Result<WalletPrincipal?>> Withdraw(Guid id, decimal amount)
  {
    try
    {
      logger.LogInformation("Withdrawing from wallet with Id '{id}' with {amount}", id, amount);
      var wallet = await db.Wallets.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (wallet is null)
        return wallet?.ToPrincipal();

      if (wallet.WithdrawReserve < amount)
        return new InsufficientBalance(
          "Insufficient balance to withdraw",
          wallet.UserId,
          wallet.Id,
          amount,
          Accounts.WithdrawReserve.Id
        ).ToException();

      wallet.WithdrawReserve -= amount;
      await db.SaveChangesAsync();
      return wallet.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to withdrawal from wallet with Id: {id} with {amount}",
        id,
        amount
      );
      throw;
    }
  }

  public async Task<Result<WalletPrincipal?>> CancelWithdraw(Guid id, decimal amount)
  {
    try
    {
      logger.LogInformation("Reverting withdraw from wallet '{id}' with {amount}", id, amount);
      var wallet = await db.Wallets.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (wallet is null)
        return wallet?.ToPrincipal();

      if (wallet.WithdrawReserve < amount)
        return new InsufficientBalance(
          "Insufficient balance in withdraw reserve to perform this action",
          wallet.UserId,
          wallet.Id,
          amount,
          Accounts.WithdrawReserve.Id
        ).ToException();

      wallet.WithdrawReserve -= amount;
      wallet.Usable += amount;
      await db.SaveChangesAsync();
      return wallet.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Reverting withdraw from wallet {id} with {amount}", id, amount);
      throw;
    }
  }

  public async Task<Result<WalletPrincipal?>> Deposit(Guid id, decimal amount)
  {
    try
    {
      logger.LogInformation("Depositing into wallet with Id '{id}' with {amount}", id, amount);
      var wallet = await db.Wallets.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (wallet is null)
        return wallet?.ToPrincipal();
      wallet.Usable += amount;
      await db.SaveChangesAsync();
      return wallet.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to deposit into wallet with Id: {id} with {amount}", id, amount);
      throw;
    }
  }

  public async Task<Result<WalletPrincipal?>> Collect(Guid id, decimal amount)
  {
    try
    {
      logger.LogInformation("Collecting from wallet with Id '{id}' with {amount}", id, amount);
      var wallet = await db.Wallets.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (wallet is null)
        return wallet?.ToPrincipal();

      if (wallet.Usable < amount)
        return new InsufficientBalance(
          "Insufficient balance to collect",
          wallet.UserId,
          wallet.Id,
          amount,
          Accounts.Usable.Id
        ).ToException();
      wallet.Usable -= amount;

      await db.SaveChangesAsync();
      return wallet.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Collecting from wallet with Id: {id} with {amount}", id, amount);
      throw;
    }
  }

  public async Task<Result<WalletPrincipal?>> BookStart(Guid id, decimal amount)
  {
    try
    {
      logger.LogInformation("Start Booking with wallet '{id}' with {amount}", id, amount);
      var wallet = await db.Wallets.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (wallet is null)
        return wallet?.ToPrincipal();

      if (wallet.Usable < amount)
        return new InsufficientBalance(
          "Insufficient balance to start booking",
          wallet.UserId,
          wallet.Id,
          amount,
          Accounts.Usable.Id
        ).ToException();

      wallet.Usable -= amount;
      wallet.BookingReserve += amount;

      await db.SaveChangesAsync();
      return wallet.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to deposit into wallet with Id: {id} with {amount}", id, amount);
      throw;
    }
  }

  public async Task<Result<WalletPrincipal?>> BookEnd(Guid id, decimal revert, decimal collect)
  {
    try
    {
      logger.LogInformation(
        "End booking with wallet '{id}' with {revert} reverted and {collect} collected",
        id,
        revert,
        collect
      );
      var wallet = await db.Wallets.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (wallet is null)
        return wallet?.ToPrincipal();

      var amount = revert + collect;

      if (wallet.BookingReserve < amount)
        return new InsufficientBalance(
          "Insufficient balance to end booking",
          wallet.UserId,
          wallet.Id,
          amount,
          Accounts.BookingReserve.Id
        ).ToException();
      wallet.BookingReserve -= amount;
      wallet.Usable += revert;

      await db.SaveChangesAsync();
      return wallet.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to end booking with wallet '{id}' {revert} reverted and {collect} collected",
        id,
        revert,
        collect
      );
      throw;
    }
  }
}
