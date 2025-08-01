using App.Error.V1;
using App.Modules.Users.Data;
using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Passenger;
using Domain.User;
using EntityFramework.Exceptions.Common;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Passengers.Data;

public class PassengerRepository(MainDbContext db, ILogger<PassengerRepository> logger)
  : IPassengerRepository
{
  public async Task<Result<IEnumerable<PassengerPrincipal>>> Search(PassengerSearch search)
  {
    try
    {
      logger.LogInformation("Searching for Passenger with '{@Search}'", search);

      var query = db.Passengers.AsQueryable();
      if (!string.IsNullOrWhiteSpace(search.UserId))
        query = query.Where(x => x.UserId == search.UserId);

      if (!string.IsNullOrWhiteSpace(search.Name))
        query = query.Where(x => EF.Functions.ILike(x.FullName, $"%{search.Name}%"));

      var result = await query.Skip(search.Skip).Take(search.Limit).ToArrayAsync();

      return result.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed search for Passenger with {@Search}", search);
      return e;
    }
  }

  public async Task<Result<Passenger?>> Get(string? userId, Guid id)
  {
    try
    {
      logger.LogInformation(
        "Retrieving Passenger with Id '{Id}' under User '{UserId}'",
        id,
        userId
      );
      var user = await db
        .Passengers.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .Include(x => x.User)
        .FirstOrDefaultAsync();
      return user?.ToDomain();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed retrieving Passenger with Id '{Id}' under User '{UserId}'",
        id,
        userId
      );
      return e;
    }
  }

  public async Task<Result<PassengerPrincipal>> Create(string userId, PassengerRecord record)
  {
    try
    {
      logger.LogInformation("Creating Passenger: {@Record}", record.ToJson());

      var data = new PassengerData { UserId = userId };
      data.UpdateData(record);

      var r = db.Passengers.Add(data);
      await db.SaveChangesAsync();
      return r.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(
        e,
        "Failed to create Passenger under User '{UserId}': {@Record} due to conflict with existing record",
        userId,
        record.ToJson()
      );

      return new EntityConflict(
        $"Failed to create Passenger under User '{userId}' due to conflicting with existing record",
        typeof(PassengerPrincipal)
      ).ToException();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to create Passenger under User '{UserId}': {@Record}",
        userId,
        record.ToJson()
      );
      return e;
    }
  }

  public async Task<Result<PassengerPrincipal?>> Update(string? userId, Guid id, PassengerRecord v2)
  {
    try
    {
      logger.LogInformation(
        "Updating Passenger '{Id}' under User '{UserId}' with: {@Record}",
        id,
        userId,
        v2.ToJson()
      );
      var v1 = await db
        .Passengers.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .FirstOrDefaultAsync();

      if (v1 == null)
        return (PassengerPrincipal?)null;

      var v3 = v1.UpdateData(v2);

      var updated = db.Passengers.Update(v3);
      await db.SaveChangesAsync();
      return updated.Entity.ToPrincipal();
    }
    catch (UniqueConstraintException e)
    {
      logger.LogError(
        e,
        "Failed to create Passenger '{Id}' under User '{UserId}': {@Record} due to conflict with existing record",
        id,
        userId,
        v2.ToJson()
      );
      return new EntityConflict(
        $"Failed to create Passenger '{id}' under User '{userId}' due to conflicting with existing record",
        typeof(PassengerPrincipal)
      ).ToException();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to create Passenger '{Id}' under User '{UserId}': {@Record}",
        id,
        userId,
        v2.ToJson()
      );
      return e;
    }
  }

  public async Task<Result<Unit?>> Delete(string? userId, Guid id)
  {
    try
    {
      logger.LogInformation("Deleting Passenger '{Id}' under User '{UserId}'", id, userId);
      var a = await db
        .Passengers.Where(x => x.Id == id && (userId == null || x.UserId == userId))
        .FirstOrDefaultAsync();
      if (a == null)
        return (Unit?)null;

      db.Passengers.Remove(a);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Passenger '{Id}' under User '{UserId}'", id, userId);
      return e;
    }
  }
}
