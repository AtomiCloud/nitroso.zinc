using System.Net.Mime;
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
  CreateUserReqValidator createUserReqValidator,
  UpdateUserReqValidator updateUserReqValidator,
  UserSearchQueryValidator userSearchQueryValidator,
  IAuthHelper h
) : AtomiControllerBase(h)
{
  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpGet]
  public async Task<ActionResult<IEnumerable<UserPrincipalRes>>> Search([FromQuery] SearchUserQuery query)
  {
    var x = await userSearchQueryValidator
      .ValidateAsyncResult(query, "Invalid SearchUserQuery")
      .ThenAwait(q => service.Search(q.ToDomain()))
      .Then(x => x.Select(u => u.ToRes()).ToResult());
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

    if (sub == null) this.ReturnNullableResult(nr, new EntityNotFound("User Not Found", typeof(User), sub ?? "none"));

    var user = await this.GuardAsync(sub)
      .ThenAwait(_ => service.GetById(sub!))
      .Then(x => x?.ToRes(), Errors.MapAll);

    return this.ReturnNullableResult(user, new EntityNotFound(
      "User Not Found", typeof(User), sub ?? "none"));
  }

  [Authorize, HttpGet("{id}")]
  public async Task<ActionResult<UserRes>> GetById(string id)
  {
    var user = await this.GuardOrAnyAsync(id, AuthRoles.Field, AuthRoles.Admin)
      .ThenAwait(_ => service.GetById(id))
      .Then(x => x?.ToRes(), Errors.MapAll);

    return this.ReturnNullableResult(user, new EntityNotFound(
      "User Not Found", typeof(User), id));
  }

  [Authorize, HttpGet("username/{username}")]
  public async Task<ActionResult<UserRes>> GetByUsername(string username)
  {
    var user = await service.GetByUsername(username)
      .Then(x => x?.ToRes(), Errors.MapAll)
      .Then(x => this.GuardOrAll(x?.Principal?.Id ?? "", AuthRoles.Field, AuthRoles.Admin)
        .Then(_ => x, Errors.MapAll)
      );
    return this.ReturnNullableResult(user, new EntityNotFound(
      "User Not Found", typeof(User), username));
  }

  [Authorize, HttpGet("exist/{username}")]
  public async Task<ActionResult<UserExistRes>> Exist(string username)
  {
    var exist = await service.Exists(username)
      .Then(x => new UserExistRes(x), Errors.MapAll);
    return this.ReturnResult(exist);
  }

  [Authorize, HttpPost]
  public async Task<ActionResult<UserPrincipalRes>> Create([FromBody] CreateUserReq req)
  {
    var id = this.Sub();
    if (id == null)
    {
      Result<UserPrincipalRes> x = new Unauthenticated(
        "You are not authenticated"
      ).ToException();
      return this.ReturnResult(x);
    }

    var user = await createUserReqValidator
      .ValidateAsyncResult(req, "Invalid CreateUserReq")
      .ThenAwait(x => service.Create(id, x.ToRecord()))
      .Then(x => x.ToRes(), Errors.MapAll);
    return this.ReturnResult(user);
  }

  [Authorize, HttpPut("{id}")]
  public async Task<ActionResult<UserPrincipalRes>> Update(string id, [FromBody] UpdateUserReq req)
  {
    var user = await this.GuardAsync(id)
      .ThenAwait(_ => updateUserReqValidator.ValidateAsyncResult(req, "Invalid UpdateUserReq"))
      .ThenAwait(x => service.Update(id, x.ToRecord()))
      .Then(x => (x?.ToRes()).ToResult());

    return this.ReturnNullableResult(user, new EntityNotFound(
      "User Not Found", typeof(UserPrincipal), id));
  }

  [Authorize(Policy = AuthPolicies.OnlyAdmin), HttpDelete("{id}")]
  public async Task<ActionResult> Delete(string id)
  {
    var user = await service.Delete(id);
    return this.ReturnUnitNullableResult(user, new EntityNotFound(
      "User Not Found", typeof(UserPrincipal), id));
  }
}
