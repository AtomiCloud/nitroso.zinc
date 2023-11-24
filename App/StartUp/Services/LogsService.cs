using App.StartUp.Options.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;


namespace App.StartUp.Services;

public static class LogsService
{
  public static OpenTelemetryLoggerOptions AddLogsService(
    this OpenTelemetryLoggerOptions builder,
    IOptionsMonitor<LogsOption> options)
  {
    if (options.CurrentValue.Exporter?.Console?.Enabled == true)
      builder.AddConsoleExporter();

    if (options.CurrentValue.Exporter?.Otlp?.Enabled == true)

      builder.AddOtlpExporter(
        o =>
        {
          var otlp = options.CurrentValue.Exporter.Otlp;
          o.Endpoint = new Uri(otlp.Endpoint);
          o.BatchExportProcessorOptions.MaxExportBatchSize = otlp.BatchSize;
          o.BatchExportProcessorOptions.MaxQueueSize = otlp.QueueSize;
          o.BatchExportProcessorOptions.ScheduledDelayMilliseconds = otlp.Delay;
          o.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = otlp.Timeout;
          o.ExportProcessorType = otlp.ProcessorType switch
          {
            "Simple" => ExportProcessorType.Simple,
            "Batch" => ExportProcessorType.Batch,
            _ => throw new ApplicationException("Invalid Exporter Type"),
          };
          o.Headers = otlp.Headers;
          o.Protocol = otlp.Protocol switch
          {
            "Grpc" => OtlpExportProtocol.Grpc,
            "HttpProtobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new ApplicationException("Invalid Exporter Protocol"),
          };
        }
      );
    return builder;
  }
}
