namespace App.StartUp.Services;

public static class ServiceExtension
{
  public static IServiceCollection AutoTrace<T>(this IServiceCollection service) where T : notnull
  {
    return service.Decorate<T>(TraceDecorator<T>.Create);
  }
}
