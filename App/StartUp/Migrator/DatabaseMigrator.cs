using App.StartUp.Options;
using App.StartUp.Registry;
using App.Utility;
using CSharp_Result;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace App.StartUp.Migrator;

public class DatabaseMigrator(
  IServiceProvider serviceProvider,
  IOptionsMonitor<Dictionary<string, DatabaseOption>> db,
  ILogger<DatabaseMigrator> logger
)
{
  public async Task<Result<IEnumerable<Unit>>> Migrate()
  {
    var results = await db
      .CurrentValue.Select(x => this.MigrateDatabase(x.Key, x.Value))
      .AwaitAll();
    return results.ToResultOfSeq();
  }

  private async Task<Result<Unit>> MigrateDatabase(string key, DatabaseOption o)
  {
    if (!o.AutoMigrate)
    {
      logger.LogInformation("Database migration skipped: {DatabaseName}", key);
      return new Unit();
    }

    logger.LogInformation("Preparing Database migration: {DatabaseName}", key);
    using var scope = serviceProvider.CreateScope();
    try
    {
      var t = Databases.List[key];
      var d = scope.ServiceProvider.GetRequiredService(t);
      if (d is not DbContext dbContext)
      {
        var ex = new ApplicationException("DbContext is not a DbContext");
        logger.LogCritical(ex, "Database migration failed: {Message}", ex.Message);
        throw ex;
      }

      return await o
        .Timeout.TryFor(
          async tries =>
          {
            logger.LogInformation(
              "Attempting to contact database '{Database}': Attempt #{Tries}",
              key,
              tries
            );
            try
            {
              var canConnect = await dbContext.Database.CanConnectAsync();
              if (canConnect)
              {
                logger.LogInformation("Successfully contacted database '{Database}'", key);
                return canConnect;
              }

              logger.LogInformation("Cannot Connect '{Database}': {Message}", key, "Unknown");
              logger.LogInformation("Waiting for new attempt for '{Database}'...", key);
              return canConnect;
            }
            catch (Exception e)
            {
              logger.LogError(e, "Cannot Connect '{Database}': {Message}", key, e.Message);
              logger.LogInformation("Waiting for new attempt for '{Database}'...", key);
              return false;
            }
          },
          () =>
          {
            var ex = new ApplicationException("Failed to contact DB");
            logger.LogCritical(ex, "Failed to contact DB '{Key}'", key);
            return ex;
          }
        )
        .ThenAwait(async _ =>
        {
          try
          {
            logger.LogInformation("Attempting to migrate database '{Database}'", key);
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Successfully migrated database '{Database}'", key);
            return (Result<Unit>)new Unit();
          }
          catch (Exception e)
          {
            logger.LogCritical(e, "Failed to migrate DB '{Key}'", key);
            return (Result<Unit>)e;
          }
        });
    }
    catch (Exception e)
    {
      logger.LogCritical(e, "Database migration failed: {Message}", e.Message);
      return e;
    }
  }
}
