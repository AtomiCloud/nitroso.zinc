using App.StartUp.Options;
using App.StartUp.Options.Metrics;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;

namespace App.StartUp.Services;

public static class MetricService
{
  public static OpenTelemetryBuilder AddMetricService(
    this OpenTelemetryBuilder builder,
    IOptionsMonitor<MetricOption> metrics,
    string meter
    )
  {
    builder.WithMetrics(options =>
    {
      options.AddMeter(meter);
      var instrument = metrics.CurrentValue.Instrument;

      if (instrument?.Runtime == true)
      {
        options.AddRuntimeInstrumentation();
      }

      if (instrument?.Process == true)
      {
        options.AddProcessInstrumentation();
      }

      if (instrument?.AspNetCore == true)
      {
        options.AddAspNetCoreInstrumentation();
      }

      if (instrument?.HttpClient == true)
        options.AddHttpClientInstrumentation();

      var exporters = metrics.CurrentValue.Exporter;
      if (exporters?.Console?.Enabled == true)
      {
        var console = exporters.Console;
        options.AddConsoleExporter((exporter, metricsReader) =>
        {
          metricsReader.PeriodicExportingMetricReaderOptions
            .ExportIntervalMilliseconds = console.ExportInterval;
        });
      }

      if (exporters?.Otlp?.Enabled == true)
      {
        var oltp = exporters.Otlp;
        options.AddOtlpExporter((exporterOptions, metricReaderOptions) =>
        {
          exporterOptions.Endpoint = new Uri(oltp.Endpoint);
          exporterOptions.BatchExportProcessorOptions.MaxExportBatchSize = oltp.BatchSize;
          exporterOptions.BatchExportProcessorOptions.MaxQueueSize = oltp.QueueSize;
          exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds = oltp.Delay;
          exporterOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = oltp.Timeout;
          exporterOptions.ExportProcessorType = oltp.ProcessorType switch
          {
            "Simple" => ExportProcessorType.Simple,
            "Batch" => ExportProcessorType.Batch,
            _ => throw new ApplicationException("Invalid Exporter Type"),
          };
          exporterOptions.Headers = oltp.Headers;
          exporterOptions.Protocol = oltp.Protocol switch
          {
            "Grpc" => OtlpExportProtocol.Grpc,
            "HttpProtobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new ApplicationException("Invalid Exporter Protocol"),
          };
          metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds
            = oltp.ExportInterval;
        });
      }
    });
    return builder;
  }
}
