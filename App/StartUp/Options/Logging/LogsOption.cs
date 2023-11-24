namespace App.StartUp.Options.Logging;

public class LogsOption
{
  public const string Key = "Logs";

  public LogsExporterOption? Exporter { get; set; } = null;
}
