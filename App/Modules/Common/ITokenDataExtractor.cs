using CSharp_Result;

namespace App.Modules.Common;

public record UserToken(string? Email, bool? EmailVerified, string[]? Roles);

public interface ITokenDataExtractor
{
  Task<Result<UserToken>> ExtractFromToken(string? idToken, string? accessToken);
}
