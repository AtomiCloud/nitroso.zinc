using System.Text;
using App.StartUp.BlockStorage;
using CSharp_Result;
using Flurl;
using MimeDetective;
using Minio.DataModel.Args;

namespace App.Modules.Common;

public interface IFileRepository
{
  Task<Result<string>> Save(string store, string dir, string name, byte[] content, bool appendExt);
  Task<Result<string>> Save(string store, string dir, string name, string content, bool appendExt);
  Task<Result<string>> Save(string store, string dir, string name, Stream content, bool appendExt);
  Task<Result<string>> Link(string store, string key);
  Task<Result<string>> SignedLink(string store, string key, int seconds);
}

public class FileRepository(IBlockStorageFactory factory, ContentInspector inspector)
  : IFileRepository
{
  private IBlockStorage Store(string key) => factory.Get(key);

  public Task<Result<string>> Save(
    string store,
    string dir,
    string name,
    byte[] content,
    bool appendExt
  )
  {
    return this.Save(store, dir, name, new MemoryStream(content), appendExt);
  }

  public Task<Result<string>> Save(
    string store,
    string dir,
    string name,
    string content,
    bool appendExt
  )
  {
    return this.Save(
      store,
      dir,
      name,
      new MemoryStream(Encoding.UTF8.GetBytes(content)),
      appendExt
    );
  }

  public async Task<Result<string>> Save(
    string store,
    string dir,
    string name,
    Stream content,
    bool appendExt
  )
  {
    var s = this.Store(store);
    content.Position = 0;
    var d = inspector.Inspect(content).MaxBy(x => x.Points)?.Definition;

    if (d == null)
      return new FormatException("Unknown file format");
    var mime = d.File.MimeType;
    var ext = d.File.Extensions.FirstOrDefault();
    if (mime == null || ext == null)
      return new FormatException("Unknown file format");
    content.Position = 0;

    var n = Path.Combine(dir, name) + "." + (appendExt ? ext : "");
    var put = new PutObjectArgs()
      .WithBucket(s.Bucket)
      .WithObject(n)
      .WithStreamData(content)
      .WithContentType(mime)
      .WithObjectSize(-1);

    var a = await s.WriteClient.PutObjectAsync(put).ConfigureAwait(false);

    return a.ObjectName;
  }

  public Task<Result<string>> Link(string store, string key)
  {
    var s = this.Store(store);
    var uri = new UriBuilder
    {
      Port = s.Port,
      Host = s.Host,
      Scheme = s.Scheme,
      Path = Url.Combine(s.Bucket, key),
    };
    return Task.FromResult<Result<string>>(uri.Uri.ToString());
  }

  public async Task<Result<string>> SignedLink(string store, string key, int expiry)
  {
    var s = this.Store(store);

    var pgo = new PresignedGetObjectArgs().WithBucket(s.Bucket).WithObject(key).WithExpiry(expiry);
    return await s.ReadClient.PresignedGetObjectAsync(pgo);
  }
}
