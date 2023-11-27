using App.Modules.Passengers.Data;
using App.Modules.Users.Data;
using App.StartUp.Services;
using Domain.Passenger;
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


    return s;
  }
}
