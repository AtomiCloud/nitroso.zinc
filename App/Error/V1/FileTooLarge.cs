using System.ComponentModel;
using System.Text.Json.Serialization;
using Humanizer;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description("The file size is too large to be accepted by the API")]
public class FileTooLarge : IDomainProblem
{
  public FileTooLarge() { }

  public FileTooLarge(string detail, long receivedSize, long acceptedSize)
  {
    this.Detail = detail;
    this.ReceivedSize = receivedSize;
    this.AcceptedSize = acceptedSize;
  }

  [JsonIgnore, JsonSchemaIgnore]
  public string Id { get; } = "invalid_file_ext";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "Invalid File Extension";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;

  [Description("The file size of the file uploaded")]
  public long ReceivedSize { get; set; } = 0;

  [Description("The file size accepted by the API")]
  public long AcceptedSize { get; set; } = 0;

  [Description("The file size of the file uploaded in human friendly format")]
  public string ReceivedSizeFriendly => this.ReceivedSize.Bits().Humanize();

  [Description("The file size accepted by the API in human friendly format")]
  public string AcceptedSizeFriendly => this.AcceptedSize.Bits().Humanize();
}
