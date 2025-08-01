using System.ComponentModel.DataAnnotations;

namespace App.StartUp.Options;

public class BlockStorageEndpoint
{
  [Required, MinLength(1)]
  public string Host { get; set; } = string.Empty;

  [Required, Range(0, ushort.MaxValue)]
  public int Port { get; set; } = 9000;

  [Required, AllowedValues("http", "https")]
  public string Scheme { get; set; } = "http";
}

public class BlockStorageOption
{
  public const string Key = "BlockStorage";

  [Required]
  public BlockStorageEndpoint Write { get; set; } = new();

  [Required]
  public BlockStorageEndpoint Read { get; set; } = new();

  [Required, MinLength(1)]
  public string AccessKey { get; set; } = string.Empty;

  [Required, MinLength(1)]
  public string SecretKey { get; set; } = string.Empty;

  [Required]
  public bool UseSSL { get; set; } = false;

  [Required]
  public bool EnsureBucketCreation { get; set; } = false;

  [Required]
  public string Bucket { get; set; } = string.Empty;

  [Required, AllowedValues("Private", "Public")]
  public string Policy { get; set; } = "Private";
}
