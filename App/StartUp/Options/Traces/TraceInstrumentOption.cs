namespace App.StartUp.Options.Traces;

public class TraceInstrumentOption
{
  public TraceInstrumentAspNetOption? AspNetCore { get; set; } = null;
  public TraceInstrumentHttpClientOption? HttpClient { get; set; } = null;

  public TraceInstrumentGrpcClientOption? GrpcClient { get; set; } = null;

  public TraceInstrumentSqlClientOption? SqlClient { get; set; } = null;

  public TraceInstrumentEFCoreOption? EFCore { get; set; } = null;
}
