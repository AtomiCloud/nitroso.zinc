using Microsoft.AspNetCore.Authorization;

namespace App.StartUp.Services.Auth;

public class HasAnyHandler(ILogger<AuthorizationHandler<HasAnyRequirement>> logger) : AuthorizationHandler<HasAnyRequirement>
{
  protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
    HasAnyRequirement requirement)
  {
    // Split the scopes string into an array
    var field = requirement.Field == "roles"
      ? "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
      : requirement.Field;

    var scopes = context.User
      .FindAll(c => c.Type == field && c.Issuer == requirement.Issuer)?
      .Select(x => x.Value);


    if (scopes == null)
    {
      logger.LogInformation("No scopes found in the token");
      return Task.CompletedTask;
    }

    // Succeed if the scope array contains the required scope
    if (requirement.Scope.Any(s => scopes.Contains(s)))
      context.Succeed(requirement);
    else
      logger.LogInformation("No matching scopes. Field: {RequireField}  Needed: {@Require}, Token: {@Token}", requirement.Field, requirement.Scope, scopes);

    return Task.CompletedTask;
  }
}
