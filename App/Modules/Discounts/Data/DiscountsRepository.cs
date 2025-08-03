using App.StartUp.Database;
using App.Utility;
using CSharp_Result;
using Domain.Discount;
using Microsoft.EntityFrameworkCore;

namespace App.Modules.Discounts.Data;

public class DiscountRepository(MainDbContext db, ILogger<DiscountRepository> logger)
  : IDiscountRepository
{
  public async Task<Result<IEnumerable<DiscountPrincipal>>> Search(DiscountSearch search)
  {
    try
    {
      logger.LogInformation("Searching for discounts with {@Search}", search);

      var query = db.Discounts.AsQueryable();

      if (search.Search is not null)
        query = query.Where(x =>
          EF.Functions.ILike(x.Name, "%" + search.Search + "%")
          || EF.Functions.ILike(x.Description, "%" + search.Search + "%")
        );
      if (search.Disabled is not null)
        query = query.Where(x => x.Disabled == search.Disabled);
      if (search.DiscountType is not null)
      {
        var dt = search.DiscountType?.ToData();
        query = query.Where(x => x.DiscountType == dt);
      }

      if (search.MatchMode is not null)
      {
        var mm = search.MatchMode?.ToData();
        query = query.Where(x => x.Target.MatchMode == mm);
      }

      if (search.MatchTarget is not null)
      {
        var mt = search.MatchTarget;
        query = query.Where(x =>
          x.Target.Matches.Any(y => mt.Contains(y.Value)) || x.Target.MatchMode == "none"
        );
      }

      var r = await query.ToArrayAsync();

      return r.Select(x => x.ToPrincipal()).ToResult();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Error searching for discounts");
      throw;
    }
  }

  public async Task<Result<DiscountPrincipal?>> Get(Guid id)
  {
    try
    {
      logger.LogInformation("Retrieving discount with Id '{Id}'", id);
      var discount = await db.Discounts.Where(x => x.Id == id).FirstOrDefaultAsync();
      return discount?.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Error searching for discounts");
      throw;
    }
  }

  public async Task<Result<DiscountPrincipal>> Create(DiscountRecord record, DiscountTarget target)
  {
    try
    {
      logger.LogInformation(
        "Creating Discount: {@Record} {@Target}",
        record.ToJson(),
        target.ToJson()
      );

      var data = new DiscountData { Disabled = false };
      data.UpdateData(record);
      data.UpdateData(target);

      var r = db.Discounts.Add(data);
      await db.SaveChangesAsync();

      return r.Entity.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to create Discount '{@Record}' with target {@Target}",
        record.ToJson(),
        target.ToJson()
      );
      throw;
    }
  }

  public async Task<Result<DiscountPrincipal?>> Update(
    Guid id,
    DiscountStatus? status,
    DiscountRecord? record,
    DiscountTarget? target
  )
  {
    try
    {
      logger.LogInformation(
        "Updating Discount '{Id}', with Status: {@DiscountStatus}, Record: {@DiscountRecord}, Target: {@Target}",
        id,
        status?.ToJson() ?? "null",
        record?.ToJson() ?? "null",
        target?.ToJson() ?? "null"
      );

      var data = await db.Discounts.Where(x => x.Id == id).FirstOrDefaultAsync();

      if (data == null)
        return (DiscountPrincipal?)null;

      if (record != null)
        data.UpdateData(record);
      if (target != null)
        data.UpdateData(target);
      if (status != null)
        data.UpdateData(status);

      await db.SaveChangesAsync();

      return data.ToPrincipal();
    }
    catch (Exception e)
    {
      logger.LogError(
        e,
        "Failed to create Discount '{@Record}' with target {@Target}",
        record.ToJson(),
        target.ToJson()
      );
      throw;
    }
  }

  public async Task<Result<Unit?>> Delete(Guid id)
  {
    try
    {
      logger.LogInformation("Deleting Discount '{Id}'", id);
      var a = await db.Discounts.Where(x => x.Id == id).FirstOrDefaultAsync();
      if (a == null)
        return (Unit?)null;

      db.Discounts.Remove(a);
      await db.SaveChangesAsync();
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogError(e, "Failed to delete Discount '{Id}'", id);
      return e;
    }
  }
}
