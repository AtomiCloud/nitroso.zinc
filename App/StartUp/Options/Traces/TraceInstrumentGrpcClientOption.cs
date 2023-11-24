namespace App.StartUp.Options.Traces;

public class TraceInstrumentGrpcClientOption
{
  public bool Enabled { get; set; } = false;

  public bool SuppressDownstreamInstrumentation { get; set; } = false;
}
