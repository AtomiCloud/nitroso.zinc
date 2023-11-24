using System.ComponentModel;
using System.Text.Json.Serialization;
using App.Modules.Common;

namespace App.Error.V1;

[Description("This error represents the entity by the given identifier could not be found")]
public class EntityNotFound : IDomainProblem
{
  public EntityNotFound() { }

  public EntityNotFound(string detail, Type type, string requestIdentifier)
  {
    this.Detail = detail;
    this.RequestIdentifier = requestIdentifier;
    this.AssemblyQualifiedName = type.AssemblyQualifiedName ?? "Unknown";
    this.TypeName = type.FullName ?? "Unknown";
  }

  [JsonIgnore]
  public string Id { get; } = "entity_not_found";

  [JsonIgnore]
  public string Title { get; } = "Entity Not Found";

  [JsonIgnore]
  public string Version { get; } = "v1";

  public string Detail { get; } = string.Empty;

  [Description("The identifier of the requested entity, which could not be found")]
  public string RequestIdentifier { get; } = string.Empty;

  [Description("The Full Name of the type of entity that could not be found")]
  public string TypeName { get; } = string.Empty;

  [Description("The AssemblyQualifiedName of the entity that could not be found")]
  public string AssemblyQualifiedName { get; } = string.Empty;
}
