using App.Modules.Passengers.Data;
using App.Modules.Users.Data;
using App.StartUp.Options;
using App.StartUp.Services;
using Domain.Passenger;
using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace App.StartUp.Database;

public class MainDbContext(IOptionsMonitor<Dictionary<string, DatabaseOption>> options)
  : DbContext
{
  public const string Key = "MAIN";

  public DbSet<UserData> Users { get; set; }

  public DbSet<PassengerData> Passengers { get; set; }


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

    var passenger = modelBuilder.Entity<PassengerData>();
    passenger.HasIndex(x => new { x.UserId, x.PassportNumber })
      .IsUnique();



  }
}
