using CSharp_Result;
using Domain.Payment;

namespace App.Modules.Payments;

// Recurring gateway-fee sync: what the manual POST payment/gateway-fees/sync
// endpoint does, on a schedule, so fee data accrues without an admin ever
// triggering it. Each tick drains the pending backlog in bounded batches
// (the per-source gateway calls stay sequential), so the first run after a
// deploy backfills all history over a few hours. Every failure is logged and
// retried on the next tick — this worker must never take the API down.
public class GatewayFeeSyncWorker(
  IServiceScopeFactory scopeFactory,
  ILogger<GatewayFeeSyncWorker> logger
) : BackgroundService
{
  public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    // yield so host startup never waits on the first sync
    await Task.Yield();
    using var timer = new PeriodicTimer(Interval);
    do
    {
      try
      {
        // the sync stack is scoped (DbContext), so each run gets a fresh scope
        await using var scope = scopeFactory.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<GatewayFeeSyncRunner>();
        var run = await runner.Drain(GatewayFeeSyncRunner.MaxBatchesPerRun, stoppingToken);
        if (run.IsSuccess())
        {
          var report = run.SuccessOrDefault();
          logger.LogInformation(
            "Gateway fee sync run complete: {Synced} synced over {Batches} batch(es), "
              + "{Missing} still missing",
            report.Synced,
            report.Batches,
            report.Missing
          );
        }
        else
        {
          logger.LogError(
            run.FailureOrDefault(),
            "Gateway fee sync run failed; retrying next tick"
          );
        }
      }
      catch (Exception e)
      {
        logger.LogError(e, "Gateway fee sync run crashed; retrying next tick");
      }
    } while (await timer.WaitForNextTickAsync(stoppingToken));
  }
}
