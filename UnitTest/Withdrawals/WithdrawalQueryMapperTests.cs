using App.Modules.Withdrawals.API.V1;
using Domain.Withdrawal;
using FluentAssertions;

namespace UnitTest.Withdrawals;

public class WithdrawalQueryMapperTests
{
  public static IEnumerable<object?[]> ValidStatuses() =>
    Enum.GetNames<WithdrawStatus>()
      .Select(status => new object?[] { status })
      .Append(new object?[] { null });

  [Fact]
  public void Export_query_maps_all_filters_and_uses_safe_domain_pagination_defaults()
  {
    var id = Guid.Parse("4d63ea4e-d9a0-43f1-bfc0-234319569a20");
    var query = new ExportWithdrawalQuery(
      id,
      "user-123",
      "admin-456",
      12.34m,
      56.78m,
      "Completed",
      "31-12-2025",
      "01-01-2025"
    );

    var search = query.ToDomain();

    search.Id.Should().Be(id);
    search.UserId.Should().Be("user-123");
    search.CompleterId.Should().Be("admin-456");
    search.Min.Should().Be(12.34m);
    search.Max.Should().Be(56.78m);
    search.Status.Should().Be(WithdrawStatus.Completed);
    search.Before.Should().Be(new DateOnly(2025, 12, 31));
    search.After.Should().Be(new DateOnly(2025, 1, 1));
    search.Limit.Should().Be(20);
    search.Skip.Should().Be(0);
  }

  [Fact]
  public void Search_query_maps_all_filters_and_explicit_pagination()
  {
    var id = Guid.Parse("fd05cd1b-aeef-4e10-aee3-4703ec1059c9");
    var query = new SearchWithdrawalQuery(
      id,
      "user-123",
      "admin-456",
      12.34m,
      56.78m,
      "RequireManualIntervention",
      "31-12-2025",
      "01-01-2025",
      50,
      10
    );

    var search = query.ToDomain();

    search.Id.Should().Be(id);
    search.UserId.Should().Be("user-123");
    search.CompleterId.Should().Be("admin-456");
    search.Min.Should().Be(12.34m);
    search.Max.Should().Be(56.78m);
    search.Status.Should().Be(WithdrawStatus.RequireManualIntervention);
    search.Before.Should().Be(new DateOnly(2025, 12, 31));
    search.After.Should().Be(new DateOnly(2025, 1, 1));
    search.Limit.Should().Be(50);
    search.Skip.Should().Be(10);
  }

  [Fact]
  public void Search_query_uses_pagination_defaults_when_values_are_omitted()
  {
    var search = new SearchWithdrawalQuery(null, null, null, null, null, null, null, null, null, null)
      .ToDomain();

    search.Limit.Should().Be(20);
    search.Skip.Should().Be(0);
  }

  [Theory]
  [MemberData(nameof(ValidStatuses))]
  public void Both_query_validators_accept_every_known_status_and_null(string? status)
  {
    var search = new SearchWithdrawalQuery(null, null, null, null, null, status, null, null, null, null);
    var export = new ExportWithdrawalQuery(null, null, null, null, null, status, null, null);

    new SearchWithdrawalQueryValidator().Validate(search).IsValid.Should().BeTrue();
    new ExportWithdrawalQueryValidator().Validate(export).IsValid.Should().BeTrue();
  }

  [Fact]
  public void Both_query_validators_reject_an_unknown_status()
  {
    var search = new SearchWithdrawalQuery(null, null, null, null, null, "Bogus", null, null, null, null);
    var export = new ExportWithdrawalQuery(null, null, null, null, null, "Bogus", null, null);

    new SearchWithdrawalQueryValidator().Validate(search).IsValid.Should().BeFalse();
    new ExportWithdrawalQueryValidator().Validate(export).IsValid.Should().BeFalse();
  }
}
