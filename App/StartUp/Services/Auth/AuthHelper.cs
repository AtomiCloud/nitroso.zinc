using System.Security.Claims;
using App.StartUp.Options.Auth;
using Microsoft.Extensions.Options;

namespace App.StartUp.Services.Auth;

public class AuthHelper(IOptionsMonitor<AuthOption> authOption)
{
  private string Issuer => authOption.CurrentValue.Settings.Issuer;

  private IEnumerable<string> FieldToScope(ClaimsPrincipal user, string field)
  {
    var f = field == "roles"
      ? "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
      : field;
    var s = user
      .FindAll(c => c.Type == field && c.Issuer == this.Issuer)?
      .Select(x => x.Value);
    if (field == "scope")
      s = s?.SelectMany(x => x.Split(' '));
    return s ?? [];
  }

  public bool HasAll(ClaimsPrincipal? user, string field, params string[] scopes)
  {

    var s = this.FieldToScope(user, field);
    return scopes.All(scope => s.Contains(scope));
  }

  public bool HasAny(ClaimsPrincipal? user, string field, params string[] scopes)
  {
    var s = this.FieldToScope(user, field);
    return scopes.Any(scope => s.Contains(scope));
  }
}
