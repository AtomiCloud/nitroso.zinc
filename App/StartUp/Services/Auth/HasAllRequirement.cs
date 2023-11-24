using Microsoft.AspNetCore.Authorization;

namespace App.StartUp.Services.Auth;

public class HasAllRequirement : IAuthorizationRequirement
{
  public string Issuer { get; }

  public string Field { get; }
  public IEnumerable<string> Scope { get; }

  public HasAllRequirement(string issuer, string field, string scope)
  {
    this.Field = field ?? throw new ArgumentNullException(nameof(field));
    this.Scope = new[] { scope } ?? throw new ArgumentNullException(nameof(scope));
    this.Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
  }

  public HasAllRequirement(string issuer, string field, params string[] scope)
  {
    this.Field = field ?? throw new ArgumentNullException(nameof(field));
    this.Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    this.Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
  }

  public HasAllRequirement(string issuer, string field, IEnumerable<string> scope)
  {
    this.Field = field ?? throw new ArgumentNullException(nameof(field));
    this.Scope = scope ?? throw new ArgumentNullException(nameof(scope));
    this.Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
  }
}
