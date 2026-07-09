using App.Modules.Milestones.API.V1;
using FluentAssertions;

namespace UnitTest.Milestones;

// Milestones mark snatching-algorithm changes; an admin creates them with a
// dd-MM-yyyy date (the API-wide standard format) and a short label
// (non-empty, <= 256 chars)
public class MilestoneValidatorTests
{
  private readonly CreateMilestoneReqValidator validator = new();

  [Fact]
  public void Valid_date_and_label_pass()
  {
    var result = validator.Validate(new CreateMilestoneReq("09-07-2026", "Switched to algo v3"));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void Label_at_the_256_char_limit_passes()
  {
    var result = validator.Validate(new CreateMilestoneReq("09-07-2026", new string('a', 256)));
    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void Label_over_256_chars_fails()
  {
    var result = validator.Validate(new CreateMilestoneReq("09-07-2026", new string('a', 257)));
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "Label");
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Empty_or_whitespace_label_fails(string label)
  {
    var result = validator.Validate(new CreateMilestoneReq("09-07-2026", label));
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "Label");
  }

  [Theory]
  [InlineData("not-a-date")]
  [InlineData("40-13-2026")]
  [InlineData("2026-07-09")] // ISO order — the API takes dd-MM-yyyy
  public void Invalid_date_fails(string date)
  {
    var result = validator.Validate(new CreateMilestoneReq(date, "algo change"));
    result.IsValid.Should().BeFalse();
    result.Errors.Should().Contain(e => e.PropertyName == "Date");
  }
}
