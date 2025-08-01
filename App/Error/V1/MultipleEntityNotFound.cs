using System.ComponentModel;
using Newtonsoft.Json;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description(
  "This error represents an error that multiple entities could not be found during a batch request"
)]
public class MultipleEntityNotFound : IDomainProblem
{
  public MultipleEntityNotFound() { }

  public MultipleEntityNotFound(string detail, Type type, string[] notfound, string[] found)
  {
    this.Detail = detail;
    this.AssemblyQualifiedName = type.AssemblyQualifiedName ?? "Unknown";
    this.TypeName = type.FullName ?? "Unknown";
    this.RequestIdentifiers = notfound;
    this.FoundRequestIdentifiers = found;
  }

  [JsonIgnore, JsonSchemaIgnore]
  public string Id { get; } = "multiple_entity_not_found";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "Multiple Entity Not Found";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;

  [Description("All identifiers of the requested entity, that could not be found")]
  public string[] RequestIdentifiers { get; } = [];

  [Description("All identifiers of the requested entity, that could be found")]
  public string[] FoundRequestIdentifiers { get; } = [];

  [Description("The Full Name of the type of entities that could not be found")]
  public string TypeName { get; } = string.Empty;

  [Description("The AssemblyQualifiedName of the entities that could not be found")]
  public string AssemblyQualifiedName { get; } = string.Empty;
}
