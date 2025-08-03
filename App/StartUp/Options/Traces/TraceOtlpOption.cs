using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Traces;

public class TraceOtlpOption
{
  public bool Enabled { get; set; } = false;

  [Required, Url]
  public string Endpoint { get; set; } = string.Empty;

  [Range(0, int.MaxValue)]
  public int Timeout { get; set; } = 0;

  [Range(0, int.MaxValue)]
  public int BatchSize { get; set; } = 0;

  [Range(0, int.MaxValue)]
  public int QueueSize { get; set; } = 0;

  [Range(0, int.MaxValue)]
  public int Delay { get; set; } = 0;

  [AllowedValues("Batch", "Simple")]
  public string ProcessorType { get; set; } = "Batch";

  [AllowedValues("Grpc", "HttpProtobuf")]
  public string Protocol { get; set; } = "Grpc";

  [Required]
  public string Headers { get; set; } = string.Empty;
}
