namespace App.StartUp.Options.Traces;

public class TraceInstrumentSqlClientOption
{
  public bool Enabled { get; set; } = false;

  public bool RecordException { get; set; } = false;

  public bool SetDbStatementForStoredProcedure { get; set; } = true;

  public bool EnableConnectionLevelAttributes { get; set; } = false;

  public bool SetDbStatementForText { get; set; } = false;
}
