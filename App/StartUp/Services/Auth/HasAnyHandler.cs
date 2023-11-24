using Microsoft.AspNetCore.Authorization;

namespace App.StartUp.Services.Auth;

public class HasAnyHandler : AuthorizationHandler<HasAnyRequirement>
{
  protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
    HasAnyRequirement requirement)
  {
    // Split the scopes string into an array
    var scopes = context.User.FindFirst(c => c.Type == "scope" && c.Issuer == requirement.Issuer)?.Value
      .Split(' ');

    if (scopes == null) return Task.CompletedTask;

    // Succeed if the scope array contains the required scope
    if (requirement.Scope.Any(s => scopes.Contains(s)))
      context.Succeed(requirement);

    return Task.CompletedTask;
  }
}
