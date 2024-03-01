using System.Security.Cryptography;
using System.Text;
using App.StartUp.Options;
using Microsoft.Extensions.Options;

namespace App.Modules.System;

public interface IEncryptor
{
  string Encrypt(string plainText);
  string Decrypt(string cipherText);
}

public class Encryptor(IOptions<EncryptionOption> options) : IEncryptor
{
  public string Encrypt(string plainText)
  {
    var key = options.Value.Secret;
    var iv = RandomNumberGenerator.GetBytes(16);


    using var aes = Aes.Create();

    var encryptor = aes.CreateEncryptor(Encoding.UTF8.GetBytes(key), iv);

    using var memoryStream = new MemoryStream();
    using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
    using (var streamWriter = new StreamWriter(cryptoStream))
    {
      streamWriter.Write(plainText);
    }
    var array = memoryStream.ToArray();

    return Convert.ToBase64String(array) + ":" + Convert.ToBase64String(iv);
  }

  public string Decrypt(string cipherText)
  {
    var key = options.Value.Secret;

    var ct = cipherText.Split(':');
    var buffer = Convert.FromBase64String(ct[0]);
    var iv = Convert.FromBase64String(ct[1]);

    using var aes = Aes.Create();
    var decryptor = aes.CreateDecryptor(Encoding.UTF8.GetBytes(key), iv);

    using var memoryStream = new MemoryStream(buffer);
    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
    using var streamReader = new StreamReader(cryptoStream);
    return streamReader.ReadToEnd();
  }
}
