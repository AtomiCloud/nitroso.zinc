using MimeDetective;

namespace App.StartUp.Services;

public static class MimeDetectionService
{
  public static IServiceCollection AddMimeDetectionService(this IServiceCollection service)
  {
    var inspector = new ContentInspectorBuilder
    {
      Definitions = MimeDetective.Definitions.Default.All(),
    }.Build();

    service.AddSingleton<ContentInspector>(s => inspector).AutoTrace<ContentInspector>();
    return service;
  }
}
