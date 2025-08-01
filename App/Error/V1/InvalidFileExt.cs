using System.ComponentModel;
using System.Text.Json.Serialization;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description("The file type (Extension) of the file uploaded is not accepted")]
public class InvalidFileExt : IDomainProblem
{
  public InvalidFileExt() { }

  public InvalidFileExt(string detail, string receivedFileType, string[] acceptedFileTypes)
  {
    this.Detail = detail;
    this.ReceivedFileType = receivedFileType;
    this.AcceptedFileTypes = acceptedFileTypes;
  }

  [JsonIgnore, JsonSchemaIgnore]
  public string Id { get; } = "invalid_file_ext";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "Invalid File Extension";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;

  [Description("The file type (Extension) of the file uploaded that was detected")]
  public string ReceivedFileType { get; set; } = string.Empty;

  [Description("The list of accepted Extension Types that allowed for this upload")]
  public string[] AcceptedFileTypes { get; set; } = [];
}
