using System.ComponentModel;
using System.Text.Json.Serialization;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description("The files uploaded were not named correctly or not in the correct number")]
public class InvalidFileUpload : IDomainProblem
{
  public InvalidFileUpload() { }

  public InvalidFileUpload(string detail)
  {
    this.Detail = detail;
  }

  [JsonIgnore, JsonSchemaIgnore] public string Id { get; } = "invalid_file_upload";

  [JsonIgnore, JsonSchemaIgnore] public string Title { get; } = "Invalid File Upload";

  [JsonIgnore, JsonSchemaIgnore] public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore] public string Detail { get; } = string.Empty;
}
