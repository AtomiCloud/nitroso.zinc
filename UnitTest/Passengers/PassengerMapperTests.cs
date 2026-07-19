using App.Modules.Passengers.API.V1;
using FluentAssertions;

namespace UnitTest.Passengers;

public class PassengerMapperTests
{
  [Theory]
  [InlineData("  John Tan  ", "John Tan")]
  [InlineData("\tAlice Lim\t", "Alice Lim")]
  [InlineData("\n Bob \r", "Bob")]
  [InlineData("No Trim Needed", "No Trim Needed")]
  public void CreateReq_ToRecord_trims_full_name(string input, string expected)
  {
    var req = new CreatePassengerReq(input, "M", "31-08-2030", "A1234567");
    req.ToRecord().FullName.Should().Be(expected);
  }

  [Theory]
  [InlineData("  A1234567  ", "A1234567")]
  [InlineData("\tB7654321 ", "B7654321")]
  public void CreateReq_ToRecord_trims_passport_number(string input, string expected)
  {
    var req = new CreatePassengerReq("John Tan", "M", "31-08-2030", input);
    req.ToRecord().PassportNumber.Should().Be(expected);
  }

  [Fact]
  public void UpdateReq_ToRecord_trims_full_name_and_passport()
  {
    var req = new UpdatePassengerReq("  Jane Doe  ", "F", "31-08-2030", "  C1112223  ");
    var record = req.ToRecord();
    record.FullName.Should().Be("Jane Doe");
    record.PassportNumber.Should().Be("C1112223");
  }

  [Fact]
  public void CreateReq_Normalize_trims_name_and_passport()
  {
    var req = new CreatePassengerReq("  John Tan \t", "M", "31-08-2030", "\tA1234567  ");
    var normalized = req.Normalize();
    normalized.FullName.Should().Be("John Tan");
    normalized.PassportNumber.Should().Be("A1234567");
    // Other fields untouched.
    normalized.Gender.Should().Be("M");
    normalized.PassportExpiry.Should().Be("31-08-2030");
  }

  [Fact]
  public void Normalize_is_null_safe_so_missing_fields_still_reach_the_validator()
  {
    var req = new CreatePassengerReq(null!, "M", "31-08-2030", null!);
    var normalized = req.Normalize();
    normalized.FullName.Should().BeNull();
    normalized.PassportNumber.Should().BeNull();
  }

  // Regression: whitespace-padded passports/names are rejected by the field
  // regex, so normalization MUST happen before validation (as the controller
  // now does), not only inside ToRecord.
  [Fact]
  public void Padded_passport_fails_validation_raw_but_passes_after_normalize()
  {
    var validator = new CreatePassengerReqValidator();
    var req = new CreatePassengerReq("John Tan", "M", "31-08-2030", "  A1234567  ");

    validator.Validate(req).IsValid.Should().BeFalse("passport regex rejects surrounding spaces");
    validator.Validate(req.Normalize()).IsValid.Should().BeTrue("normalization trims before validation");
  }

  [Fact]
  public void Padded_name_passes_validation_after_normalize()
  {
    var validator = new UpdatePassengerReqValidator();
    var req = new UpdatePassengerReq("  Jane Doe  ", "F", "31-08-2030", "C1112223");

    validator.Validate(req.Normalize()).IsValid.Should().BeTrue();
  }
}
