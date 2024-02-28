using App.StartUp.Options.Traces;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace App.StartUp.Services;

public static class TraceService
{
  public static OpenTelemetryBuilder AddTraceService(
    this OpenTelemetryBuilder builder,
    IOptionsMonitor<TraceOption> traces)
  {
    builder.WithTracing(options =>
    {
      options.AddSource("AutoTrace.*");
      var instrument = traces.CurrentValue.Instrument;

      if (instrument?.EFCore?.Enabled == true)
      {
        var ef = instrument.EFCore;
        options.AddEntityFrameworkCoreInstrumentation(o =>
        {
          o.SetDbStatementForText = ef.SetDbStatementForText;
          o.SetDbStatementForStoredProcedure = ef.SetDbStatementForStoredProcedure;
        });
      }

      if (instrument?.AspNetCore?.Enabled == true)
      {
        var asp = instrument.AspNetCore;
        options.AddAspNetCoreInstrumentation(
          o =>
          {
            o.RecordException = asp.RecordException;
          });
      }

      if (instrument?.HttpClient?.Enabled == true)
      {
        options.AddHttpClientInstrumentation(o =>
        {
          o.RecordException = instrument.HttpClient.RecordException;
        });
      }

      if (instrument?.GrpcClient?.Enabled == true)
      {
        options.AddGrpcClientInstrumentation(o =>
        {
          o.SuppressDownstreamInstrumentation = instrument.GrpcClient.SuppressDownstreamInstrumentation;
        });
      }

      if (instrument?.SqlClient?.Enabled == true)
      {
        options.AddSqlClientInstrumentation(o =>
        {
          var sql = instrument.SqlClient;
          o.RecordException = sql.RecordException;
          o.SetDbStatementForStoredProcedure = sql.SetDbStatementForStoredProcedure;
          o.EnableConnectionLevelAttributes = sql.EnableConnectionLevelAttributes;
          o.SetDbStatementForText = sql.SetDbStatementForText;
        });
      }

      var exporters = traces.CurrentValue.Exporter;
      if (exporters?.Console?.Enabled == true)
      {
        options.AddConsoleExporter();
      }

      if (exporters?.Otlp?.Enabled == true)
      {
        var oltp = exporters.Otlp;
        options.AddOtlpExporter(o =>
        {
          o.Endpoint = new Uri(oltp.Endpoint);
          o.BatchExportProcessorOptions.MaxExportBatchSize = oltp.BatchSize;
          o.BatchExportProcessorOptions.MaxQueueSize = oltp.QueueSize;
          o.BatchExportProcessorOptions.ScheduledDelayMilliseconds = oltp.Delay;
          o.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = oltp.Timeout;
          o.ExportProcessorType = oltp.ProcessorType switch
          {
            "Simple" => ExportProcessorType.Simple,
            "Batch" => ExportProcessorType.Batch,
            _ => throw new ApplicationException("Invalid Exporter Type"),
          };
          o.Headers = oltp.Headers;
          o.Protocol = oltp.Protocol switch
          {
            "Grpc" => OtlpExportProtocol.Grpc,
            "HttpProtobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new ApplicationException("Invalid Exporter Protocol"),
          };
        });
      }
    });
    return builder;
  }
}
