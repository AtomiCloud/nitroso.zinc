using App.StartUp.Migrator;

namespace App.StartUp.BlockStorage;

public class BlockStorageHostedService(
  BlockStorageMigrator blockStorageMigrator,
  ILogger<BlockStorageHostedService> logger,
  IHostApplicationLifetime lifetime
) : IHostedService
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    var r = await blockStorageMigrator.Migrate();
    if (r.IsFailure())
    {
      var e = r.FailureOrDefault();
      logger.LogCritical(e, "Failed to migrate or contact block storage");
      Environment.ExitCode = 1;
      lifetime.StopApplication();
    }
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    return Task.CompletedTask;
  }
}
