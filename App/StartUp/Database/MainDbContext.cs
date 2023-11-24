using App.Modules.Users.Data;
using App.StartUp.Options;
using App.StartUp.Services;
using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace App.StartUp.Database;

public class MainDbContext(IOptionsMonitor<Dictionary<string, DatabaseOption>> options)
  : DbContext
{
  public const string Key = "MAIN";

  public DbSet<UserData> Users { get; set; } = null!;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    optionsBuilder
      .AddPostgres(options.CurrentValue, Key)
      .UseExceptionProcessor();
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    var user = modelBuilder.Entity<UserData>();
    user.HasIndex(x => x.Username).IsUnique();
  }
}
