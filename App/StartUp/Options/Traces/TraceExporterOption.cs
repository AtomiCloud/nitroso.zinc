namespace App.StartUp.Options.Traces;

public class TraceExporterOption
{
  public TraceConsoleOption? Console { get; set; } = null;

  public TraceOtlpOption? Otlp { get; set; } = null;
}
