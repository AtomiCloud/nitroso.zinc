namespace App.StartUp.Options.Traces;

public class TraceOption
{
  public const string Key = "Trace";

  public TraceExporterOption? Exporter { get; set; } = null;

  public TraceInstrumentOption? Instrument { get; set; } = null;
}
