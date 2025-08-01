using System.Security.Claims;
using App.StartUp.Options.Auth;
using Microsoft.Extensions.Options;

namespace App.StartUp.Services.Auth;

public interface IAuthHelper
{
  bool HasAll(ClaimsPrincipal? user, string field, params string[] scopes);

  bool HasAny(ClaimsPrincipal? user, string field, params string[] scopes);

  IEnumerable<string> FieldToScope(ClaimsPrincipal? user, string field);

  ILogger<IAuthHelper> Logger { get; }
}

public class AuthHelper(IOptionsMonitor<AuthOption> authOption, ILogger<IAuthHelper> logger)
  : IAuthHelper
{
  private string? Issuer => authOption.CurrentValue.Settings?.Issuer;

  public IEnumerable<string> FieldToScope(ClaimsPrincipal? user, string field)
  {
    var f =
      field == "roles" ? "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" : field;
    var s = user?.FindAll(c => c.Type == f && c.Issuer == this.Issuer)?.Select(x => x.Value);
    if (field == "scope")
      s = s?.SelectMany(x => x.Split(' '));
    return s ?? [];
  }

  public ILogger<IAuthHelper> Logger => logger;

  public bool HasAll(ClaimsPrincipal? user, string field, params string[] scopes)
  {
    var s = this.FieldToScope(user, field);
    var r = scopes.All(scope => s.Contains(scope));

    if (!r)
      logger.LogInformation(
        "No matching scopes. Field: {RequireField}  Needed: {@Require}, Token: {@Token}",
        field,
        scopes,
        s
      );
    return r;
  }

  public bool HasAny(ClaimsPrincipal? user, string field, params string[] scopes)
  {
    var s = this.FieldToScope(user, field);
    var r = scopes.Any(scope => s.Contains(scope));
    if (!r)
      logger.LogInformation(
        "No matching scopes. Field: {RequireField}  Needed: {@Require}, Token: {@Token}",
        field,
        scopes,
        s
      );
    return r;
  }
}
