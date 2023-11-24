namespace App.StartUp.Options.Metrics;

public class MetricInstrumentOption
{

  public bool? AspNetCore { get; set; }
  public bool? HttpClient { get; set; }
  public bool? Process { get; set; }
  public bool? Runtime { get; set; }
}
