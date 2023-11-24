using System.ComponentModel;
using App.Modules.Common;
using Newtonsoft.Json;

namespace App.Error.V1;

[Description("This error occurs when the server cannot detect any special signature to indicate the file type")]
public class UnknownFileType : IDomainProblem
{
  public UnknownFileType() { }

  public UnknownFileType(string detail)
  {
    this.Detail = detail;
  }
  [JsonIgnore]
  public string Id { get; } = "unknown_file_type";
  [JsonIgnore]
  public string Title { get; } = "Unknown File Type";
  [JsonIgnore]
  public string Version { get; } = "v1";
  [JsonIgnore]
  public string Detail { get; } = string.Empty;
}
