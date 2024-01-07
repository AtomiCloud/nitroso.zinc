using System.Transactions;
using CSharp_Result;
using Domain;

namespace App.Modules.System;

public class TransactionManager(ILogger<TransactionManager> logger) : ITransactionManager
{
  public async Task<Result<T>> Start<T>(Func<Task<Result<T>>> func)
  {
    using var scope = new TransactionScope(TransactionScopeOption.Required,
      new TransactionOptions() { IsolationLevel = IsolationLevel.RepeatableRead },
      TransactionScopeAsyncFlowOption.Enabled);
    var r = await func();
    if (r.IsSuccess())
    {
      // force eval
      var s = r.Get();
      scope.Complete();
      return s;
    }

    var e = r.FailureOrDefault();
    logger.LogError(e, "Transaction failed");
    return e;
  }
}
