namespace App.StartUp.Options.Metrics;

public class MetricExporterOption
{
  public MetricConsoleOption? Console { get; set; } = null;

  public MetricOtlpOption? Otlp { get; set; } = null;
}
