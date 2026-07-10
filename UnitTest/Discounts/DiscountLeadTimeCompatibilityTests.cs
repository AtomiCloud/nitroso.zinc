using App.Modules.Discounts.API.V1;
using FluentAssertions;

namespace UnitTest.Discounts;

public class DiscountLeadTimeCompatibilityTests
{
  [Fact]
  public void Deprecated_under_field_maps_to_the_early_buy_threshold()
  {
    var domain = Request(under: 48).ToDomain();

    domain.LeadTimeAtLeastHours.Should().Be(48);
    var response = domain.ToRes();
    response.LeadTimeAtLeastHours.Should().Be(48);
    response.LeadTimeUnderHours.Should().Be(48);
  }

  [Fact]
  public void New_field_takes_precedence_when_the_alias_agrees()
  {
    var request = Request(atLeast: 24, under: 24);

    DiscountRecordReqValidator.LeadTimeFieldsAgree(request).Should().BeTrue();
    request.ToDomain().LeadTimeAtLeastHours.Should().Be(24);
  }

  [Fact]
  public void Conflicting_new_and_deprecated_fields_are_rejected()
  {
    DiscountRecordReqValidator
      .LeadTimeFieldsAgree(Request(atLeast: 48, under: 6))
      .Should()
      .BeFalse();
  }

  private static DiscountRecordReq Request(int? atLeast = null, int? under = null) =>
    new(
      "early bird",
      "buy earlier",
      0.1m,
      "Percentage",
      null,
      null,
      null,
      null,
      atLeast,
      under,
      null,
      null
    );
}
