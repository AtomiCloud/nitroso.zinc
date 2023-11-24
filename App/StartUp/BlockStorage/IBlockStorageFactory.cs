namespace App.StartUp.BlockStorage;

public interface IBlockStorageFactory
{
  IBlockStorage Get(string key);
}
