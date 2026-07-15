using System.Net.Mime;
using System.Text.RegularExpressions;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Users.API.V1;

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class UserController(
  IUserService service,
  IPartnerEconomicsRepository partnerEconomicsRepo,
  CreateUserReqValidator createUserReqValidator,
  UpdateUserReqValidator updateUserReqValidator,
  UserSearchQueryValidator userSearchQueryValidator,
  ITokenDataExtractor tokenDataExtractor,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  // ExtraRoles role format: 1-64 chars of lowercase letters, digits, '_' or
  // '-' — matching how JWT roles are written, so pricing unions compare
  // apples to apples
  private static readonly Regex RoleFormat = new("^[a-z0-9_-]{1,64}$", RegexOptions.Compiled);

  private static Result<string> ValidRole(string role) =>
    RoleFormat.IsMatch(role)
      ? role
      : new ValidationError(
        "Invalid role",
        new Dictionary<string, string[]>
        {
          ["Role"] =
          [
            "Role must be 1-64 characters of lowercase letters, digits, '_' or '-' (^[a-z0-9_-]{1,64}$)",
          ],
        }
      ).ToException();

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet]
  public async Task<ActionResult<IEnumerable<UserPrincipalRes>>> Search(
    [FromQuery] SearchUserQuery query
  )
  {
    var x = await userSearchQueryValidator
      .ValidateAsyncResult(query, "Invalid SearchUserQuery")
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()).ToResult());
    return this.ReturnResult(x);
  }

  // Users explicitly tagged as partners through ExtraRoles. Role matching is
  // case-insensitive so legacy/manual role casing cannot hide a partner.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("partners")]
  public async Task<ActionResult<IEnumerable<PartnerUserRes>>> Partners()
  {
    var x = await partnerEconomicsRepo
      .ListPartners()
      .Then(users => users.Select(u => u.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  // Monthly economics for this user's wallet only. Each event is bucketed by
  // its inclusive SGT source date; empty months are omitted.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet("{id}/pnl")]
  public async Task<ActionResult<IEnumerable<PartnerPnlRowRes>>> PartnerPnl(
    string id,
    [FromQuery] PartnerPnlQueryReq query,
    [FromServices] PartnerPnlQueryReqValidator partnerPnlValidator
  )
  {
    var x = await partnerPnlValidator
      .ValidateAsyncResult(query, "Invalid PartnerPnlQueryReq")
      .ThenAwait(q => partnerEconomicsRepo.Pnl(id, q.ToDomain()))
      .Then(rows => rows.Select(r => r.ToRes()), Errors.MapAll);
    return this.ReturnResult(x);
  }

  [Authorize, HttpGet("Me")]
  public string Me()
  {
    return this.Sub() ?? "none";
  }

  [Authorize, HttpGet("Me/All")]
  public async Task<ActionResult<UserRes>> MeAll()
  {
    var sub = this.Sub();

    Result<UserRes?> nr = (UserRes?)null;

    if (sub == null)
      this.ReturnNullableResult(
        nr,
        new EntityNotFound("User Not Found", typeof(User), sub ?? "none")
      );

    var user = await this.GuardAsync(sub)
      .ThenAwait(_ => service.GetById(sub!))
      .Then(x => x?.ToRes(), Errors.MapAll);

    return this.ReturnNullableResult(
      user,
      new EntityNotFound("User Not Found", typeof(User), sub ?? "none")
    );
  }

  [Authorize, HttpGet("{id}")]
  public async Task<ActionResult<UserRes>> GetById(string id)
  {
    var user = await this.GuardOrAnyAsync(id, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.GetById(id))
      .Then(x => x?.ToRes(), Errors.MapAll);

    return this.ReturnNullableResult(user, new EntityNotFound("User Not Found", typeof(User), id));
  }

  [Authorize, HttpGet("username/{username}")]
  public async Task<ActionResult<UserRes>> GetByUsername(string username)
  {
    var user = await service
      .GetByUsername(username)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .Then(x =>
        this.GuardOrAll(x?.Principal?.Id ?? "", AuthRoles.Field, AuthRoles.Admin)
          .Then(_ => x, Errors.MapAll)
      );
    return this.ReturnNullableResult(
      user,
      new EntityNotFound("User Not Found", typeof(User), username)
    );
  }

  [Authorize, HttpGet("exist/{username}")]
  public async Task<ActionResult<UserExistRes>> Exist(string username)
  {
    var exist = await service.Exists(username).Then(x => new UserExistRes(x), Errors.MapAll);
    return this.ReturnResult(exist);
  }

  [Authorize, HttpPost]
  public async Task<ActionResult<UserPrincipalRes>> Create([FromBody] CreateUserReq req)
  {
    var id = this.Sub();
    if (id == null)
    {
      Result<UserPrincipalRes> x = new Unauthenticated("You are not authenticated").ToException();
      return this.ReturnResult(x);
    }

    var validatedUser = await createUserReqValidator.ValidateAsyncResult(req, "Invalid CreateUserReq");
    var token = await tokenDataExtractor.ExtractFromToken(req.IdToken, req.AccessToken);

    var userRecord = from u in validatedUser
      from t in token
      select u.ToRecord(t);
    
    var user = await userRecord.ToAsyncResult()
      .ThenAwait(record => service.Create(id, record))
      .Then(x => x.ToRes(), Errors.MapAll);
    return this.ReturnResult(user);
  }

  [Authorize, HttpPut("{id}")]
  public async Task<ActionResult<UserPrincipalRes>> Update(string id, [FromBody] UpdateUserReq req)
  {
    var validatedUser = await this.GuardAsync(id)
      .ThenAwait(_ => updateUserReqValidator.ValidateAsyncResult(req, "Invalid UpdateUserReq"));
    var token = await tokenDataExtractor.ExtractFromToken(req.IdToken, req.AccessToken);
    var userRecord = from u in validatedUser
        from t in token
        select u.ToRecord(t);
    var user = await userRecord.ToAsyncResult()
      .ThenAwait(record => service.Update(id, record))
      .Then(x => (x?.ToRes()).ToResult());

    return this.ReturnNullableResult(
      user,
      new EntityNotFound("User Not Found", typeof(UserPrincipal), id)
    );
  }

  // Admin-managed extra roles for discount/pricing targeting. Writing
  // UserData.Roles directly would be silently reverted by the frontend token
  // sync (it re-reads the JWT roles on mismatch), so admin grants live in
  // ExtraRoles; pricing unions them with the JWT roles, authorization never
  // reads them.
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpPost("{id}/roles/{role}")]
  public async Task<ActionResult<UserPrincipalRes>> AddExtraRole(string id, string role)
  {
    var x = await Task.FromResult(ValidRole(role))
      .ThenAwait(r => service.AddExtraRole(id, r))
      .Then(u => u?.ToRes(), Errors.MapNone);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("User Not Found", typeof(UserPrincipal), id)
    );
  }

  // 404 when the user does not exist OR does not have the role
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id}/roles/{role}")]
  public async Task<ActionResult<UserPrincipalRes>> RemoveExtraRole(string id, string role)
  {
    var x = await Task.FromResult(ValidRole(role))
      .ThenAwait(r => service.RemoveExtraRole(id, r))
      .Then(u => u?.ToRes(), Errors.MapNone);
    return this.ReturnNullableResult(
      x,
      new EntityNotFound("User or extra role not found", typeof(UserPrincipal), id)
    );
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id}")]
  public async Task<ActionResult> Delete(string id)
  {
    var user = await service.Delete(id);
    return this.ReturnUnitNullableResult(
      user,
      new EntityNotFound("User Not Found", typeof(UserPrincipal), id)
    );
  }
}
