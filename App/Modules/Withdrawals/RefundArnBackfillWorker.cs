using CSharp_Result;
using Domain.Withdrawal;

namespace App.Modules.Withdrawals;

// Recurring Acquirer Reference Number backfill for settled card refunds.
// ARN capture happens on the settle path, but every fragment settled before
// that existed has a blank refund_arns slot in the tax export — and the
// gateway forgets a refund 2 years after creation, so the gap only widens.
// Each tick drains a bounded slice of that backlog with sequential gateway
// lookups, so the first runs after a deploy fill in history over a few hours.
// Every failure is logged and retried next tick — this worker must never take
// the API down.
public class RefundArnBackfillWorker(
  IServiceScopeFactory scopeFactory,
  ILogger<RefundArnBackfillWorker> logger
) : BackgroundService
{
  public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // yield so host startup never waits on the first drain
    await Task.Yield();
    using var timer = new PeriodicTimer(Interval);
    do
    {
      try
      {
        // the repository stack is scoped (DbContext), so each run gets a
        // fresh scope
        await using var scope = scopeFactory.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<RefundArnBackfillRunner>();
        var run = await runner.Drain(
          DateTime.UtcNow,
          RefundArnBackfillRunner.MaxBatchesPerRun,
          stoppingToken
        );
        if (run.IsSuccess())
        {
          var report = run.SuccessOrDefault();
          logger.LogInformation(
            "Refund ARN backfill complete: {Captured} captured of {Scanned} scanned over "
              + "{Batches} batch(es), {Pending} still awaiting an ARN, {Unbackfillable} "
              + "permanently unbackfillable (settled beyond the gateway's retention window)",
            report.Captured,
            report.Scanned,
            report.Batches,
            report.Pending,
            report.Unbackfillable
          );
        }
        else
        {
          logger.LogError(
            run.FailureOrDefault(),
            "Refund ARN backfill run failed; retrying next tick"
          );
        }
      }
      catch (Exception e)
      {
        logger.LogError(e, "Refund ARN backfill run crashed; retrying next tick");
      }
    } while (await timer.WaitForNextTickAsync(stoppingToken));
  }
}
