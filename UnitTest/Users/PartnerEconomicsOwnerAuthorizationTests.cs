using App.Modules.Users.API.V1;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using CSharp_Result;
using Domain.User;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTest.Users;

public class PartnerEconomicsOwnerAuthorizationTests
{
  [Fact]
  public async Task Partners_rejects_non_owner_before_repository_access()
  {
    var repository = new FakePartnerEconomicsRepository();
    var controller = Controller([AuthRoles.Admin], repository);

    var response = await controller.Partners();

    response.Result.Should().BeOfType<StatusCodeResult>()
      .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    repository.ListCalls.Should().Be(0);
  }

  [Fact]
  public async Task Partner_pnl_rejects_non_owner_before_validation_or_repository_access()
  {
    var repository = new FakePartnerEconomicsRepository();
    var controller = Controller([AuthRoles.Admin], repository);

    var response = await controller.PartnerPnl(
      "partner-1",
      new PartnerPnlQueryReq("not-a-date", null),
      new PartnerPnlQueryReqValidator()
    );

    response.Result.Should().BeOfType<StatusCodeResult>()
      .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    repository.PnlCalls.Should().Be(0);
  }

  [Fact]
  public async Task Partners_accepts_owner_role_case_insensitively()
  {
    var repository = new FakePartnerEconomicsRepository();
    var controller = Controller([AuthRoles.Admin, "OwNeR"], repository);

    var response = await controller.Partners();

    response.Result.Should().BeOfType<OkObjectResult>();
    repository.ListCalls.Should().Be(1);
  }

  [Fact]
  public async Task Partner_pnl_accepts_owner_role_case_insensitively()
  {
    var repository = new FakePartnerEconomicsRepository();
    var controller = Controller([AuthRoles.Admin, "OWNER"], repository);

    var response = await controller.PartnerPnl(
      "partner-1",
      new PartnerPnlQueryReq(null, null),
      new PartnerPnlQueryReqValidator()
    );

    response.Result.Should().BeOfType<OkObjectResult>();
    repository.PnlCalls.Should().Be(1);
  }

  private static UserController Controller(
    string[] roles,
    IPartnerEconomicsRepository repository
  ) =>
    new(null!, null!, repository, null!, null!, null!, null!, new FakeAuthHelper(roles))
    {
      ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

  private sealed class FakePartnerEconomicsRepository : IPartnerEconomicsRepository
  {
    public int ListCalls { get; private set; }

    public int PnlCalls { get; private set; }

    public Task<Result<PartnerUser[]>> ListPartners()
    {
      this.ListCalls++;
      return Task.FromResult((Result<PartnerUser[]>)Array.Empty<PartnerUser>());
    }

    public Task<Result<PartnerPnlRow[]>> Pnl(string userId, PartnerPnlQuery query)
    {
      this.PnlCalls++;
      return Task.FromResult((Result<PartnerPnlRow[]>)Array.Empty<PartnerPnlRow>());
    }
  }

  private sealed class FakeAuthHelper(string[] roles) : IAuthHelper
  {
    public bool HasAll(
      System.Security.Claims.ClaimsPrincipal? user,
      string field,
      params string[] scopes
    ) => scopes.All(scope => roles.Contains(scope));

    public bool HasAny(
      System.Security.Claims.ClaimsPrincipal? user,
      string field,
      params string[] scopes
    ) => scopes.Any(scope => roles.Contains(scope));

    public IEnumerable<string> FieldToScope(
      System.Security.Claims.ClaimsPrincipal? user,
      string field
    ) => field == AuthRoles.Field ? roles : [];

    public ILogger<IAuthHelper> Logger => NullLogger<IAuthHelper>.Instance;
  }
}
