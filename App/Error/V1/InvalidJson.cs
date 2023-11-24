using System.ComponentModel;
using Newtonsoft.Json;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description(
  "The request cannot be parsed as a JSON. This is not the default JSON validator, used mainly for multipart forms.")]
public class InvalidJson : IDomainProblem
{
  public InvalidJson() { }

  public InvalidJson(string detail, string originalString)
  {
    this.Detail = detail;
    this.InvalidString = originalString;
  }

  [JsonIgnore, JsonSchemaIgnore] public string Id { get; } = "invalid_json";

  [JsonIgnore, JsonSchemaIgnore] public string Title { get; } = "Invalid JSON";

  [JsonIgnore, JsonSchemaIgnore] public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore] public string Detail { get; } = string.Empty;

  [Description("The string that was parsed and was invalid")]
  public string InvalidString { get; } = string.Empty;
}
