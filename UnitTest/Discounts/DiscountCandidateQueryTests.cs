using App.Modules.Discounts.Data;
using FluentAssertions;

namespace UnitTest.Discounts;

public class DiscountCandidateQueryTests
{
  [Fact]
  public void Target_prefilter_keeps_every_candidate_that_can_match()
  {
    var rows = new[]
    {
      Discount("none-empty", "none"),
      Discount("all-empty", "all"),
      Discount("all-matching", "all", "vip"),
      Discount("any-matching", "any", "vip"),
      Discount("all-other", "all", "staff"),
      Discount("any-other", "any", "staff"),
      Discount("any-empty", "any"),
    };

    rows
      .AsQueryable()
      .FilterByMatchTarget(["vip", "user-1"])
      .Select(x => x.Name)
      .Should()
      .BeEquivalentTo("none-empty", "all-empty", "all-matching", "any-matching");
  }

  private static DiscountData Discount(string name, string matchMode, params string[] matches) =>
    new()
    {
      Name = name,
      Target = new DiscountTargetData
      {
        MatchMode = matchMode,
        Matches = matches
          .Select(x => new DiscountMatchData { Type = "role", Value = x })
          .ToList(),
      },
    };
}
