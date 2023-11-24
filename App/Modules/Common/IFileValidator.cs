using App.Error.V1;
using App.Utility;
using CSharp_Result;
using Humanizer;
using MimeDetective;

namespace App.Modules.Common;

public record FileValidationParam
{
  public string[]? AllowedMime { get; init; }
  public long? MaxSize { get; init; }
  public string[]? AllowedExt { get; init; }
}

public interface IFileValidator
{
  Task<Result<Stream>> Validate(IFormFile file, FileValidationParam param);

  Task<Result<Stream>> Validate(Stream stream, FileValidationParam param);
}

public class FileValidator(ContentInspector inspector, ILogger<FileValidator> logger) : IFileValidator
{
  public async Task<Result<Stream>> Validate(IFormFile file, FileValidationParam param)
  {
    using var fStream = new MemoryStream();
    await file.CopyToAsync(fStream);
    fStream.Position = 0;
    return await this.Validate(fStream, param);
  }

  public Task<Result<Stream>> Validate(Stream stream, FileValidationParam param)
  {
    stream.Position = 0;

    // Validate file
    var d = inspector
      .Inspect(stream)
      .MaxBy(x => x.Points)?.Definition;
    if (d == null)
    {
      var err = new UnknownFileType("Unknown file format");
      logger.LogError(err.ToException(), "Server cannot detect the MIME type of the file");
      return Task.FromResult<Result<Stream>>(err.ToException());
    }

    var mime = d.File.MimeType;
    var ext = d.File.Extensions.FirstOrDefault();
    if (mime == null || ext == null)
    {
      var err = new UnknownFileType("Unknown file format");
      logger.LogError(err.ToException(), "Server cannot detect the MIME type of the file");
      return Task.FromResult<Result<Stream>>(err.ToException());
    }

    logger.LogInformation("File is a '{Mime}' file with Extension: {Ext}", mime, ext);
    if (param.AllowedMime != null && !param.AllowedMime.Contains(mime))
    {
      var err = new InvalidFileType($"File must be one of '{param.AllowedMime.Humanize()}' but was '{mime}'", mime,
        param.AllowedMime);
      logger.LogError(err.ToException(), "File must be one of '{Allowed}' but was '{Mime}'",
        param.AllowedMime.Humanize(), mime);
      return Task.FromResult<Result<Stream>>(err.ToException());
    }

    if (param.AllowedExt != null && !param.AllowedExt.Any(x => d.File.Extensions.Contains(x)))
    {
      var err = new InvalidFileExt(
        $"File must be one of '{param.AllowedExt.Humanize()}' but was '{d.File.Extensions.Humanize()}'", ext,
        param.AllowedExt);
      logger.LogError(err.ToException(), "File must be one of '{Allowed}' but was '{Extension}'",
        param.AllowedExt.Humanize(), d.File.Extensions.Humanize());
      return Task.FromResult<Result<Stream>>(err.ToException());
    }

    if (param.MaxSize != null && stream.Length > param.MaxSize)
    {
      var maxSize = param.MaxSize.Value;
      var err = new FileTooLarge(
        $"File must be smaller than {maxSize.Bits().Humanize()} but received {stream.Length.Bits().Humanize()}",
        stream.Length,
        maxSize);
      logger.LogError(err.ToException(),
        "File must be smaller than {AcceptedFileSize} ({AcceptedFileSizeFriendly}) but received {ReceivedFileSize} ({ReceivedFileSizeFriendly})",
        maxSize, maxSize.Bits().Humanize(), stream.Length, stream.Length.Bits().Humanize());
      return Task.FromResult<Result<Stream>>(err.ToException());
    }

    stream.Position = 0;
    return Task.FromResult<Result<Stream>>(stream);
  }
}
