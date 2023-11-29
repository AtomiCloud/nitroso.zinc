using App.Modules.Passengers.Data;
using App.Modules.Schedules.Data;
using App.Modules.TrainBookings.Data;
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

  public DbSet<BookingData> Bookings { get; set; }

  public DbSet<ScheduleData> Schedules { get; set; }


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

    var booking = modelBuilder.Entity<BookingData>();
    booking.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
    booking.Property(x => x.CompletedAt).HasDefaultValue(null);
    booking.Property(x => x.Status).HasDefaultValue(0);
  }
}
