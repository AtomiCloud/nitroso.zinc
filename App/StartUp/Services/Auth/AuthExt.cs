using Microsoft.AspNetCore.Authorization;

namespace App.StartUp.Services.Auth;

public static class AuthExt
{
  public static void AddAllScope(this IList<IAuthorizationRequirement> req, string domain, string field,
    params string[] scopes)
  {
    req.Add(new HasAllRequirement(domain, field, scopes));
  }

  public static void AddAnyScope(this IList<IAuthorizationRequirement> req, string domain, string field,
    params string[] scopes)
  {
    req.Add(new HasAnyRequirement(domain, field, scopes));
  }
}
