using Minio;

namespace App.StartUp.BlockStorage;

public interface IBlockStorage
{
  IMinioClient ReadClient { get; }

  IMinioClient WriteClient { get; }

  string Bucket { get; }

  public string Scheme { get; }

  public string Host { get; }

  public int Port { get; }
}
