namespace App.StartUp.Options.Traces;

public class TraceInstrumentHttpClientOption
{
  public bool Enabled { get; set; } = false;
  public bool RecordException { get; set; } = false;
}
