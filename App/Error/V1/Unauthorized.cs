using System.ComponentModel;
using System.Text.Json.Serialization;
using App.Error.Common;
using App.Modules.Common;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description(
  "This error means you are authenticated but not authorized (do not have sufficient permission) to access the resource."
)]
public class Unauthorized : IDomainProblem
{
  public Unauthorized() { }

  public Unauthorized(string detail, Scope[] granted, Scope[] required)
  {
    this.Detail = detail;
    this.Granted = granted;
    this.Required = required;
  }

  [JsonIgnore, JsonSchemaIgnore]
  public string Id { get; } = "unauthorized";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "Unauthorized";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;

  [Description("The Scope(s) that was granted to the user.")]
  public Scope[] Granted { get; } = [];

  [Description("The Scope(s) that was required to access the resource.")]
  public Scope[] Required { get; } = [];
}
