using System.ComponentModel;
using System.Text.Json.Serialization;
using App.Modules.Common;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description("This error means you are not authenticated to access the resource.")]
public class Unauthenticated : IDomainProblem
{
  public Unauthenticated() { }

  public Unauthenticated(string detail)
  {
    this.Detail = detail;
  }

  [JsonIgnore, JsonSchemaIgnore]
  public string Id { get; } = "unauthenticated";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "Unauthenticated";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;
}
