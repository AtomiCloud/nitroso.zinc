using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Metrics;

public class MetricOtlpOption
{
  public bool Enabled { get; set; } = false;

  [Required, Url] public string Endpoint { get; set; } = string.Empty;

  [Range(0, int.MaxValue)] public int ExportInterval { get; set; } = 0;

  [Range(0, int.MaxValue)] public int Timeout { get; set; } = 0;

  [Range(0, int.MaxValue)] public int BatchSize { get; set; } = 0;
  [Range(0, int.MaxValue)] public int QueueSize { get; set; } = 0;
  [Range(0, int.MaxValue)] public int Delay { get; set; } = 0;

  [Required] public string Headers { get; set; } = string.Empty;

  [AllowedValues("Batch", "Simple")] public string ProcessorType { get; set; } = "Batch";

  [AllowedValues("Grpc", "HttpProtobuf")]
  public string Protocol { get; set; } = "Grpc";
}
