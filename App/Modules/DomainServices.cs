using App.Modules.Bookings;
using App.Modules.Bookings.API.V1;
using App.Modules.Bookings.Data;
using App.Modules.Passengers.Data;
using App.Modules.Schedules.Data;
using App.Modules.System;
using App.Modules.Timings.Data;
using App.Modules.Transactions.Data;
using App.Modules.Users.Data;
using App.Modules.Wallets.Data;
using App.StartUp.Services;
using Domain;
using Domain.Admin;
using Domain.Booking;
using Domain.Cost;
using Domain.Passenger;
using Domain.Schedule;
using Domain.Timings;
using Domain.Transaction;
using Domain.User;
using Domain.Wallet;

namespace App.Modules;

public static class DomainServices
{
  public static IServiceCollection AddDomainServices(this IServiceCollection s)
  {
    // USER
    s.AddScoped<IUserService, UserService>()
      .AutoTrace<IUserService>();

    s.AddScoped<IUserRepository, UserRepository>()
      .AutoTrace<IUserRepository>();

    // Passenger
    s.AddScoped<IPassengerService, PassengerService>()
      .AutoTrace<IPassengerService>();

    s.AddScoped<IPassengerRepository, PassengerRepository>()
      .AutoTrace<IPassengerRepository>();

    // Schedules
    s.AddScoped<IScheduleService, ScheduleService>()
      .AutoTrace<IScheduleService>();

    s.AddScoped<IScheduleRepository, ScheduleRepository>()
      .AutoTrace<IScheduleRepository>();

    // Timings
    s.AddScoped<ITimingService, TimingService>()
      .AutoTrace<ITimingService>();

    s.AddScoped<ITimingRepository, TimingRepository>()
      .AutoTrace<ITimingRepository>();

    // Bookings
    s.AddScoped<IBookingService, BookingService>()
      .AutoTrace<IBookingService>();

    s.AddScoped<IBookingRepository, BookingRepository>()
      .AutoTrace<IBookingRepository>();

    s.AddScoped<IBookingCdcRepository, BookingCdcRepository>()
      .AutoTrace<IBookingCdcRepository>();

    s.AddScoped<IBookingStorage, BookingStorage>()
      .AutoTrace<IBookingStorage>();

    s.AddScoped<IBookingImageEnricher, BookingImageEnricher>()
      .AutoTrace<IBookingImageEnricher>();

    // Transaction
    s.AddScoped<ITransactionService, TransactionService>()
      .AutoTrace<ITimingService>();

    s.AddScoped<ITransactionRepository, TransactionRepository>()
      .AutoTrace<ITransactionRepository>();
    s.AddScoped<ITransactionGenerator, TransactionGenerator>()
      .AutoTrace<ITransactionGenerator>();

    // Wallet
    s.AddScoped<IWalletService, WalletService>()
      .AutoTrace<IWalletService>();

    s.AddScoped<IWalletRepository, WalletRepository>()
      .AutoTrace<IWalletRepository>();

    // Refund Calculator
    s.AddScoped<IRefundCalculator, RefundCalculator>()
      .AutoTrace<IRefundCalculator>();

    // Transaction Manager
    s.AddScoped<ITransactionManager, TransactionManager>()
      .AutoTrace<ITransactionManager>();

    s.AddScoped<ICostCalculator, SimpleCostCalculator>()
      .AutoTrace<ICostCalculator>();

    // Admin
    s.AddScoped<IAdminService, AdminService>()
      .AutoTrace<IAdminService>();

    return s;
  }
}
