using App.Modules.Bookings.Data;
using App.Modules.Costs.Data;
using App.Modules.Discounts.Data;
using App.Modules.Milestones.Data;
using App.Modules.Passengers.Data;
using App.Modules.Payments.Data;
using App.Modules.Schedules.Data;
using App.Modules.Timings.Data;
using App.Modules.Transactions.Data;
using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using App.Modules.Withdrawals.Data;
using App.StartUp.Options;
using App.StartUp.Services;
using App.Utility;
using Domain.Timings;
using EntityFramework.Exceptions.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace App.StartUp.Database;

public class MainDbContext(
  IOptionsMonitor<Dictionary<string, DatabaseOption>> options,
  ILoggerFactory factory
) : DbContext
{
  public const string Key = "MAIN";

  public DbSet<PaymentData> Payments { get; set; }

  public DbSet<DiscountData> Discounts { get; set; }
  public DbSet<CostData> Costs { get; set; }

  public DbSet<CostPolicyData> CostPolicies { get; set; }

  public DbSet<WalletData> Wallets { get; set; }

  public DbSet<WithdrawalData> Withdrawals { get; set; }

  public DbSet<WithdrawalRefundData> WithdrawalRefunds { get; set; }

  public DbSet<FeeData> Fees { get; set; }

  public DbSet<TransactionData> Transactions { get; set; }

  public DbSet<UserData> Users { get; set; }

  public DbSet<PassengerData> Passengers { get; set; }

  public DbSet<BookingData> Bookings { get; set; }

  // keyless read-only projection of the booking_stats materialized view
  public DbSet<BookingStatData> BookingStats { get; set; }

  public DbSet<MilestoneData> Milestones { get; set; }

  public DbSet<PrioritySettingsData> PrioritySettings { get; set; }

  public DbSet<PriorityAccessData> PriorityAccesses { get; set; }

  public DbSet<ScheduleData> Schedules { get; set; }

  public DbSet<TimingData> Timings { get; set; }

  private static readonly string[] J2WTiming =
  [
    "05:00:00",
    "05:30:00",
    "06:00:00",
    "06:30:00",
    "07:00:00",
    "07:30:00",
    "08:45:00",
    "10:00:00",
    "11:30:00",
    "12:45:00",
    "14:00:00",
    "15:15:00",
    "16:30:00",
    "17:45:00",
    "19:00:00",
    "20:15:00",
    "21:30:00",
    "22:45:00",
  ];

  private static readonly string[] W2JTiming =
  [
    "08:30:00",
    "09:45:00",
    "11:00:00",
    "12:30:00",
    "13:45:00",
    "15:00:00",
    "16:15:00",
    "17:30:00",
    "18:45:00",
    "20:00:00",
    "21:15:00",
    "22:30:00",
    "23:45:00",
  ];

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  {
    optionsBuilder
      .UseLoggerFactory(factory)
      .AddPostgres(options.CurrentValue, Key)
      .UseExceptionProcessor();
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    var user = modelBuilder.Entity<UserData>();
    user.HasIndex(x => x.Username).IsUnique();
    // pre-existing rows get an empty array, never NULL — ExtraRoles must
    // always be safe to union with the JWT roles
    user.Property(x => x.ExtraRoles).HasDefaultValueSql("'{}'::text[]");

    var passenger = modelBuilder.Entity<PassengerData>();
    passenger.HasIndex(x => new { x.UserId, x.PassportNumber }).IsUnique();

    var booking = modelBuilder.Entity<BookingData>();
    booking.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
    booking.Property(x => x.CompletedAt).HasDefaultValue(null);
    booking.Property(x => x.Status).HasDefaultValue(0);
    // pre-existing rows have never been recovery-recycled — default 0 keeps
    // the retry cap meaningful for them without a backfill
    booking
      .Property(x => x.RecoveryRetries)
      .HasDefaultValue(0)
      .HasComment(
        "Times this booking was recycled from Recovering back to Pending (RecoverRevert); capped by Recovery:MaxRetries"
      );

    // materialized view (created via raw SQL in a migration, refreshed on
    // read by BookingRepository) — ToView keeps it out of EF migrations
    var bookingStats = modelBuilder.Entity<BookingStatData>();
    bookingStats.HasNoKey();
    bookingStats.ToView(BookingStatsView.Name);

    var milestone = modelBuilder.Entity<MilestoneData>();
    milestone.HasIndex(x => x.Date);

    var timings = modelBuilder.Entity<TimingData>();
    timings.HasData(
      new TimingData
      {
        Direction = TrainDirection.JToW.ToData(),
        Timings = J2WTiming.Select(x => x.ToTime()).ToArray(),
      },
      new TimingData
      {
        Direction = TrainDirection.WToJ.ToData(),
        Timings = W2JTiming.Select(x => x.ToTime()).ToArray(),
      }
    );

    var cost = modelBuilder.Entity<CostData>();
    cost.HasIndex(x => x.CreatedAt).IsUnique();
    // FIXED id + FIXED early timestamp: dynamic Guid.NewGuid()/UtcNow here
    // made every migration delete and re-insert this seed row with a fresh
    // CreatedAt — and since pricing is newest-CreatedAt-wins, each deploy
    // could silently revert an admin-set cost back to the seed value
    cost.HasData(
      new CostData
      {
        Id = new Guid("6dfd5a2b-37a0-4c65-9d05-1c8dbd5237a2"),
        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Cost = 14,
      }
    );

    var discount = modelBuilder.Entity<DiscountData>();
    discount.HasIndex(x => x.Name);
    discount.OwnsOne(
      x => x.Target,
      d =>
      {
        d.ToJson();
        d.OwnsMany(dt => dt.Matches);
      }
    );

    // priority-boost targeting: two owned JSON blobs, same shape and storage
    // convention as DiscountData.Target; null column = target not configured
    var prioritySettings = modelBuilder.Entity<PrioritySettingsData>();
    prioritySettings.OwnsOne(
      x => x.FreeTarget,
      d =>
      {
        d.ToJson();
        d.OwnsMany(dt => dt.Matches);
      }
    );
    prioritySettings.OwnsOne(
      x => x.AccessTarget,
      d =>
      {
        d.ToJson();
        d.OwnsMany(dt => dt.Matches);
      }
    );

    var withdrawal = modelBuilder.Entity<WithdrawalData>();
    // existing rows predate the method column and are all PayNow transfers
    withdrawal
      .Property(x => x.Method)
      .HasDefaultValue((byte)0)
      .HasComment("Payout rail: 0 = PayNow transfer, 1 = card refunds (Airwallex)");

    var withdrawalRefund = modelBuilder.Entity<WithdrawalRefundData>();
    withdrawalRefund.HasIndex(x => x.WithdrawalId);
    withdrawalRefund.HasIndex(x => x.PaymentId);
    // the gateway idempotency key is unique by construction; the index makes
    // webhook routing by request id an O(1) lookup and enforces the invariant
    withdrawalRefund.HasIndex(x => x.RequestId).IsUnique();

    var payments = modelBuilder.Entity<PaymentData>();
    payments.HasIndex(x => x.ExternalReference);
    payments.OwnsOne(
      x => x.Statuses,
      d =>
      {
        d.ToJson();
        d.OwnsMany(x => x.Statuses);
      }
    );
  }
}
