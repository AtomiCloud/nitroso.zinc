namespace App.StartUp.Options.Traces;

public class TraceInstrumentEFCoreOption
{
  public bool Enabled { get; set; } = false;

  public bool SetDbStatementForStoredProcedure { get; set; } = true;
  public bool SetDbStatementForText { get; set; } = true;
}
