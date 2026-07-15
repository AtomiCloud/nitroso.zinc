using System.Text.Json;
using App.Error.V1;
using App.Modules.Users.API.V1;
using App.StartUp.Registry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UnitTest.Users;

// argon builds its account-deletion UI against this EXACT wire shape — the
// contract was pinned BEFORE the UI exists, so a rename here can never
// silently break it. ASP.NET Core serializes with JsonSerializerDefaults.Web
// (camelCase).
public class UserWipeWireContractTests
{
  private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

  [Fact]
  public void Wipe_receipt_serializes_as_id_and_wipedAt()
  {
    var at = new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc);

    var json = JsonSerializer.Serialize(new UserWipeRes("user-1", at), Web);

    json.Should().Be("{\"id\":\"user-1\",\"wipedAt\":\"2026-07-15T08:00:00Z\"}");
  }

  [Fact]
  public void Wipe_refusal_problem_serializes_detail_userId_and_reason()
  {
    var problem = new InvalidUserWipeOperation(
      "the wallet still holds money; pay the user out fully before wiping",
      "user-1",
      "wallet_not_empty"
    );

    var json = JsonSerializer.Serialize(problem, Web);

    json.Should().Be(
      "{\"detail\":\"the wallet still holds money; pay the user out fully before wiping\","
        + "\"userId\":\"user-1\",\"reason\":\"wallet_not_empty\"}"
    );
    problem.Id.Should().Be("invalid_user_wipe_operation");
  }

  [Fact]
  public void Wipe_route_is_admin_only_POST_id_wipe()
  {
    var method = typeof(UserController).GetMethod(nameof(UserController.Wipe))!;

    var post = method.GetCustomAttributes(typeof(HttpPostAttribute), false)
      .Cast<HttpPostAttribute>()
      .Single();
    post.Template.Should().Be("{id}/wipe");

    var auth = method.GetCustomAttributes(typeof(AuthorizeAttribute), false)
      .Cast<AuthorizeAttribute>()
      .Single();
    auth.Policy.Should().Be(AuthPolicies.OnlyAdmin);
  }
}
