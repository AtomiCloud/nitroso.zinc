using Minio;

namespace App.StartUp.BlockStorage;

public class BlockStorage(IMinioClient readClient, IMinioClient writeClient, string bucket, string scheme, string host,
    int port)
  : IBlockStorage
{
  public IMinioClient ReadClient { get; } = readClient;

  public IMinioClient WriteClient { get; } = writeClient;
  public string Bucket { get; } = bucket;

  public string Scheme { get; } = scheme;

  public string Host { get; } = host;
  public int Port { get; } = port;
}


