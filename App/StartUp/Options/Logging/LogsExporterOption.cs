namespace App.StartUp.Options.Logging;

public class LogsExporterOption
{
  public LogsConsoleOption? Console { get; set; } = null;

  public LogsOtlpOption? Otlp { get; set; } = null;
}
