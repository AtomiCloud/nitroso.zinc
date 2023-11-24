using App.StartUp.BlockStorage;
using App.StartUp.Options;
using App.Utility;
using CSharp_Result;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace App.StartUp.Migrator;

public class BlockStorageMigrator(ILogger<BlockStorageMigrator> logger, IBlockStorageFactory factory, IOptionsMonitor<Dictionary<string, BlockStorageOption>> store)
{
  public async Task<Result<IEnumerable<Unit>>> Migrate()
  {
    var result = await store.CurrentValue
      .Select(x => this.MigrateBlockStorage(x.Key, x.Value))
      .AwaitAll();
    return result.ToResultOfSeq();
  }
  private async Task<Result<Unit>> MigrateBlockStorage(string key, BlockStorageOption o)
  {
    if (!o.EnsureBucketCreation)
    {
      logger.LogInformation("Bucket check skipped: {BlockStorageName}", key);
      return new Unit();
    }

    using var scope = logger.BeginScope("Check BlockStorage: {BlockStorageName}", key);
    try
    {
      var b = factory.Get(key);

      logger.LogInformation("Checking bucket for: {BlockStorageName}", key);
      var beArgs = new BucketExistsArgs().WithBucket(o.Bucket);
      var found = await b.WriteClient.BucketExistsAsync(beArgs);
      logger.LogInformation("Bucket {BucketName} Exist: {BucketExist}", key, found);
      if (found) return new Unit();

      var mbArgs = new MakeBucketArgs().WithBucket(o.Bucket);
      await b.WriteClient.MakeBucketAsync(mbArgs);
      logger.LogInformation("Bucket created: {BlockStorageName}", key);
      if (o.Policy != "Public") return new Unit();
      var policy = $$"""
  {
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Principal": {
          "AWS": [
            "*"
          ]
        },
        "Action": [
          "s3:GetObject"
        ],
        "Resource": [
          "arn:aws:s3:::{{o.Bucket}}/*"
        ]
      }
    ]
  }
  """;
      var spa = new SetPolicyArgs()
        .WithBucket(o.Bucket)
        .WithPolicy(policy);

      await b.WriteClient.SetPolicyAsync(spa).ConfigureAwait(false);
      logger.LogInformation("Configured Bucket: {BlockStorageName}", key);
      return new Unit();
    }
    catch (Exception e)
    {
      logger.LogCritical(e, "Bucket check failed: {Message}", e.Message);
      return e;
    }
  }
}
