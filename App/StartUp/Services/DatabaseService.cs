using App.StartUp.Options;
using App.StartUp.Registry;
using Microsoft.EntityFrameworkCore;

namespace App.StartUp.Services;

public static class DatabaseService
{
  public static DbContextOptionsBuilder AddPostgres(
    this DbContextOptionsBuilder service,
    Dictionary<string, DatabaseOption> o,
    string key
  )
  {
    var dbOpts = o[key];
    if (Databases.AcceptedDatabase().All(x => x != key))
      throw new ApplicationException(
        $"DatabaseOption.Key (Config File) must be in Databases.List (Class): {key}"
      );
    var connectionString =
      $"Host={dbOpts.Host};Port={dbOpts.Port};Database={dbOpts.Database};Username={dbOpts.User};Password={dbOpts.Password};";
    service.UseNpgsql(connectionString);
    return service;
  }
}
