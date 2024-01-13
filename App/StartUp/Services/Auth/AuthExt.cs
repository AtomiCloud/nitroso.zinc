using App.Error.V1;
using App.StartUp.Registry;
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

  public static Unauthorized RequirementToProblem(this HasAllRequirement requirement,
    string detail, string field, IEnumerable<string>? scopes
    )
  {
    return new Unauthorized(detail,
      scopes?.Select(s => new Scope(field, s)).ToArray() ?? [],
      requirement.Scope.Select(s => new Scope(field, s)).ToArray());
  }

  public static Unauthorized RequirementToProblem(this HasAnyRequirement requirement,
    string detail, string field, IEnumerable<string>? scopes
  )
  {
    return new Unauthorized(detail,
      scopes?.Select(s => new Scope(field, s)).ToArray() ?? [],
      requirement.Scope.Select(s => new Scope(field, s)).ToArray());
  }

  public static void SetProblem(this HttpContext context, HasAnyRequirement requirement, string detail, string field,
    IEnumerable<string>? scopes)
  {
    context.Items[Constants.ProblemContextKey] = requirement.RequirementToProblem(detail, field, scopes);
  }

  public static void SetProblem(this HttpContext context, HasAllRequirement requirement, string detail, string field,
    IEnumerable<string>? scopes)
  {
    context.Items[Constants.ProblemContextKey] = requirement.RequirementToProblem(detail, field, scopes);
  }

}
