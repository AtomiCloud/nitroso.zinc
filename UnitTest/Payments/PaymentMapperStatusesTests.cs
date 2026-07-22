using App.Modules.Payments.Data;
using FluentAssertions;

namespace UnitTest.Payments;

// The status history is an append-only list, so a status can repeat (e.g. a
// 3DS retry loop bouncing REQUIRES_PAYMENT_METHOD ↔ REQUIRES_CUSTOMER_ACTION).
// ToPrincipal must collapse repeats to the latest occurrence per status
// instead of throwing on the duplicate dictionary key — that exception 500ed
// every read AND every webhook update of such payments.
public class PaymentMapperStatusesTests
{
  private static PaymentData Make(params (string Status, DateTime Updated)[] history) =>
    new()
    {
      Id = Guid.NewGuid(),
      ExternalReference = "int_test",
      Gateway = "airwallex",
      CreatedAt = DateTime.UtcNow,
      Amount = 50m,
      CapturedAmount = 0m,
      Currency = "SGD",
      LastUpdated = DateTime.UtcNow,
      Status = history.Length == 0 ? "created" : history[^1].Status,
      Statuses = new PaymentStatusData
      {
        Statuses = history
          .Select(x => new PaymentStatusEntryData { Status = x.Status, Updated = x.Updated })
          .ToList(),
      },
      WalletId = Guid.NewGuid(),
    };

  [Fact]
  public void ToPrincipal_WithRepeatedStatuses_KeepsLatestPerStatus()
  {
    var t0 = new DateTime(2026, 7, 22, 0, 21, 0, DateTimeKind.Utc);
    var t1 = new DateTime(2026, 7, 22, 0, 31, 0, DateTimeKind.Utc);
    var t2 = new DateTime(2026, 7, 22, 5, 54, 0, DateTimeKind.Utc);

    var data = Make(
      ("created", t0),
      ("REQUIRES_PAYMENT_METHOD", t0),
      ("REQUIRES_CUSTOMER_ACTION", t0),
      ("REQUIRES_PAYMENT_METHOD", t1),
      ("REQUIRES_CUSTOMER_ACTION", t1),
      ("REQUIRES_PAYMENT_METHOD", t2),
      ("REQUIRES_CUSTOMER_ACTION", t2)
    );

    var principal = data.ToPrincipal();

    principal.Statuses.Should().HaveCount(3);
    principal.Statuses["created"].Should().Be(t0);
    principal.Statuses["REQUIRES_PAYMENT_METHOD"].Should().Be(t2);
    principal.Statuses["REQUIRES_CUSTOMER_ACTION"].Should().Be(t2);
  }

  [Fact]
  public void ToPrincipal_WithUniqueStatuses_MapsAll()
  {
    var t0 = new DateTime(2026, 7, 20, 2, 57, 0, DateTimeKind.Utc);
    var t1 = new DateTime(2026, 7, 20, 2, 59, 0, DateTimeKind.Utc);

    var data = Make(("created", t0), ("SUCCEEDED", t1));

    var principal = data.ToPrincipal();

    principal.Statuses.Should().HaveCount(2);
    principal.Statuses["created"].Should().Be(t0);
    principal.Statuses["SUCCEEDED"].Should().Be(t1);
  }

  [Fact]
  public void ToPrincipal_WithEmptyHistory_MapsEmpty()
  {
    var principal = Make().ToPrincipal();

    principal.Statuses.Should().BeEmpty();
  }
}
