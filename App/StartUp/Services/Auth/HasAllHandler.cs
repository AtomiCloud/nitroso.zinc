using App.Utility;
using Kirinnee.Helper;
using Microsoft.AspNetCore.Authorization;

namespace App.StartUp.Services.Auth;

public class HasAllHandler : AuthorizationHandler<HasAllRequirement>
{
  protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
    HasAllRequirement requirement)
  {
    // Split the scopes string into an array
    var field = requirement.Field == "roles"
      ? "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
      : requirement.Field;
    var scopes = context.User
      .FindAll(c => c.Type == field && c.Issuer == requirement.Issuer)?
      .Select(x => x.Value);

    if (requirement.Field == "scope")
      scopes = scopes?.SelectMany(x => x.Split(' '));

    if (scopes == null) return Task.CompletedTask;

    // Succeed if the scope array contains the required scope
    if (requirement.Scope.All(s => scopes.Contains(s)))
      context.Succeed(requirement);

    return Task.CompletedTask;
  }
}
