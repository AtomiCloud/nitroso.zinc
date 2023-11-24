using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options.Metrics;

public class MetricOption
{
  public const string Key = "Metrics";

  public MetricExporterOption? Exporter { get; set; } = null;

  public MetricInstrumentOption? Instrument { get; set; } = null;
}
