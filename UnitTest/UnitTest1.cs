using App.Modules.Payments.Airwallex;
using App.Modules.System;
using App.StartUp.Options;
using Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace UnitTest;

public class UnitTest1(ITestOutputHelper output)
{
  [Fact]
  public void CheckEnryptor()
  {
    var option = new EncryptionOption { Secret = "1234567812345678" };

    var o = Options.Create(option);
    var encryptor = new Encryptor(o);

    var a = encryptor.Encrypt("we love the world");

    output.WriteLine(a);

    var d = encryptor.Decrypt(a);

    d.Should().Be("we love the world");
  }

  [Fact]
  public void CheckAirwallexHmacCalculator()
  {
    var option = new PaymentOption
    {
      Airwallex = new AirwallexOption
      {
        WebhookKey = "",
        ApiKey = "",
        BaseAddress = "",
        ClientId = "",
      }
    };

    var o = Options.Create(option);

    var calculator = new AirwallexHmacCalculator(o);

    var sig = calculator.Compute("1709275233230",
        "{\"id\":\"evt_sgpdcmjs7gtvy79memm_79m0qk\",\"name\":\"payment_intent.requires_payment_method\",\"account_id\":\"acct_FHZJtJt5P3W7EUhJntm2fg\",\"accountId\":\"acct_FHZJtJt5P3W7EUhJntm2fg\",\"data\":{\"object\":{\"amount\":5.05,\"base_amount\":5.05,\"base_currency\":\"SGD\",\"captured_amount\":0,\"created_at\":\"2024-03-01T06:40:33+0000\",\"currency\":\"SGD\",\"descriptor\":\"BunnyBooker\",\"id\":\"int_sgpdcmjs7gtvy79m0qk\",\"merchant_order_id\":\"ca6007db-d5d2-4f6b-89ab-494d48b44b78\",\"request_id\":\"ca6007db-d5d2-4f6b-89ab-494d48b44b78\",\"status\":\"REQUIRES_PAYMENT_METHOD\",\"updated_at\":\"2024-03-01T06:40:33+0000\"}},\"created_at\":\"2024-03-01T06:40:33+0000\",\"createdAt\":\"2024-03-01T06:40:33+0000\",\"version\":\"2023-10-01\",\"sourceId\":\"int_sgpdcmjs7gtvy79m0qk\"}")
      .SuccessOrDefault();

    sig.Should().Be("b1955bb1ee27beaa5a6be98e35422ab78dc09c532ea469ed7faf65f7889b393e");
  }
}
