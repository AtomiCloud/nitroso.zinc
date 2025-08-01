using App.StartUp.BlockStorage;
using App.StartUp.Options;
using Minio;

namespace App.StartUp.Services;

public static class BlockStorageService
{
  public static IServiceCollection AddBlockStorage(
    this IServiceCollection services,
    Dictionary<string, BlockStorageOption> o
  )
  {
    var s = new BlockStorageFactory();
    services.AddSingleton<IBlockStorageFactory>((sp) => s).AutoTrace<IBlockStorageFactory>();
    foreach (var (k, v) in o)
    {
      var writeMc = new MinioClient()
        .WithEndpoint($"{v.Write.Host}:{v.Write.Port}")
        .WithCredentials(v.AccessKey, v.SecretKey)
        .WithSSL(v.UseSSL)
        .Build();

      var readMc = new MinioClient()
        .WithEndpoint($"{v.Read.Host}:{v.Read.Port}")
        .WithCredentials(v.AccessKey, v.SecretKey)
        .WithSSL(v.UseSSL)
        .Build();
      var wm = writeMc ?? throw new ApplicationException($"Write Minio client is null: {k}");
      var rm = readMc ?? throw new ApplicationException($"Read Minio client is null: {k}");
      var b = new BlockStorage.BlockStorage(
        rm,
        wm,
        v.Bucket,
        v.Read.Scheme,
        v.Read.Host,
        v.Read.Port
      );
      s.Add(k, b);
    }

    return services;
  }
}
