using System.ComponentModel;
using Newtonsoft.Json;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description("This error means an unique field conflict, or/and already exists.")]

public class EntityConflict : IDomainProblem
{
  public EntityConflict() { }

  public EntityConflict(string detail, Type type)
  {
    this.Detail = detail;
    this.AssemblyQualifiedName = type.AssemblyQualifiedName ?? "Unknown";
    this.TypeName = type.FullName ?? "Unknown";
  }

  [JsonIgnore, JsonSchemaIgnore]
  public string Id { get; } = "entity_conflict";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "EntityConflict";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;
  [Description("The Full Name of the type of entity that is in conflict")]
  public string TypeName { get; } = string.Empty;

  [Description("The AssemblyQualifiedName of the entity that is in conflict")]
  public string AssemblyQualifiedName { get; } = string.Empty;
}
