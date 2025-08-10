using System.Collections.Concurrent;

namespace App.StartUp.BlockStorage;

public class BlockStorageFactory : IBlockStorageFactory
{
  private readonly ConcurrentDictionary<string, IBlockStorage> _storages = [];

  public void Add(string key, IBlockStorage storage)
  {
    this._storages.TryAdd(key, storage);
  }

  public IBlockStorage Get(string key)
  {
    if (this._storages.TryGetValue(key, out var storage))
      return storage;
    throw new ApplicationException($"Block storage not found: {key}");
  }
}
