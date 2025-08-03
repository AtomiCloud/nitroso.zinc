using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.StartUp.Options.Auth;
using App.StartUp.Registry;
using CSharp_Result;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace App.Modules.Common;

public class TokenDataExtractor(
  IOptionsMonitor<AuthOption> authOption,
  ILogger<TokenDataExtractor> logger
) : ITokenDataExtractor
{
  public Task<Result<UserToken>> ExtractFromToken(string? idToken, string? accessToken)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(idToken) && string.IsNullOrWhiteSpace(accessToken))
        return Task.FromResult<Result<UserToken>>(new UserToken(null, null, null));

      var authSettings = authOption.CurrentValue.Settings;
      if (authSettings?.TokenValidation == null)
      {
        logger.LogWarning("Auth settings or token validation not configured");
        return Task.FromResult<Result<UserToken>>(new UserToken(null, null, null));
      }

      var handler = new JwtSecurityTokenHandler();
      
      string? email = null;
      bool? emailVerified = null;
      string[]? roles = null;

      // Extract email and email_verified from ID token
      if (!string.IsNullOrWhiteSpace(idToken))
      {
        var idTokenParsed = handler.ReadJwtToken(idToken);
        
        var emailClaim =
          idTokenParsed.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)
          ?? idTokenParsed.Claims.FirstOrDefault(c => c.Type == "email");

        var emailVerifiedClaim = 
          idTokenParsed.Claims.FirstOrDefault(c => c.Type == "email_verified");

        email = emailClaim?.Value;
        emailVerified = emailVerifiedClaim?.Value != null && 
                       bool.TryParse(emailVerifiedClaim.Value, out var verified) ? verified : (bool?)null;
      }

      // Extract roles from access token
      if (!string.IsNullOrWhiteSpace(accessToken))
      {
        var accessTokenParsed = handler.ReadJwtToken(accessToken);
        
        var rolesClaims = accessTokenParsed.Claims.Where(c => 
          c.Type == ClaimTypes.Role || 
          c.Type == "role" ||
          c.Type == "roles" ||
          c.Type == AuthRoles.Field 
        ).Select(c => c.Value).ToArray();

        roles = rolesClaims.Length > 0 ? rolesClaims : null;
      }

      var tokenData = new UserToken(email, emailVerified, roles);

      logger.LogInformation(
        "Successfully extracted data from tokens: Email={HasEmail}, EmailVerified={EmailVerified}, Roles={RoleCount}",
        !string.IsNullOrEmpty(email),
        emailVerified,
        roles?.Length ?? 0
      );

      return Task.FromResult<Result<UserToken>>(tokenData);
    }
    catch (Exception ex)
    {
      logger.LogWarning(ex, "Error extracting data from tokens");
      return Task.FromResult<Result<UserToken>>(new UserToken(null, null, null));
    }
  }
}
