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
}
