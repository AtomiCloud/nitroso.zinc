using App.Modules.Bookings.API.V1;
using App.Modules.Bookings.Data;
using App.Modules.Passengers.Data;
using App.Modules.Schedules.Data;
using App.Modules.Timings.Data;
using App.Modules.Users.Data;
using App.StartUp.Services;
using Domain.Booking;
using Domain.Passenger;
using Domain.Schedule;
using Domain.Timings;
using Domain.User;

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




    return s;
  }
}
