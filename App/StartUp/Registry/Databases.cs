using App.StartUp.Database;

namespace App.StartUp.Registry;

public static class Databases
{
  public static readonly Dictionary<string, Type> List = new()
  {
    { MainDbContext.Key, typeof(MainDbContext) },
  };

  public static IEnumerable<string> AcceptedDatabase()
  {
    return [.. List.Keys];
  }
}
