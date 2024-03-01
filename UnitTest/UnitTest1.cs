using App.Modules.System;
using App.StartUp.Options;
using Domain;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace UnitTest;

public class UnitTest1(ITestOutputHelper output)
{

  [Fact]
  public void CheckEnryptor()
  {
    var option = new EncryptionOption
    {
      Secret = "1234567812345678"
    };

    var o = Options.Create(option);
    var encryptor = new Encryptor(o);

    var a = encryptor.Encrypt("we love the world");

    output.WriteLine(a);

    var d = encryptor.Decrypt(a);

    d.Should().Be("we love the world");

  }
}
