namespace App.StartUp.Options.Traces;

public class TraceInstrumentAspNetOption
{
  public bool Enabled { get; set; } = false;
  public bool GrpcSupport { get; set; } = true;
  public bool RecordException { get; set; } = false;
}
