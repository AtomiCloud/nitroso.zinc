using System.Net.Mime;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Database;
using App.StartUp.Registry;
using App.Utility;
using Asp.Versioning;
using CSharp_Result;
using Domain.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using StackExchange.Redis.Extensions.Core.Abstractions;

namespace App.Modules.Users.API.V1;


public record FileRequest(string Name, IFormFile File);

[ApiVersion(1.0)]
[ApiController]
[Consumes(MediaTypeNames.Application.Json)]
[Route("api/v{version:apiVersion}/[controller]")]
public class UserController(
  IUserService service,
  CreateUserReqValidator createUserReqValidator,
  UpdateUserReqValidator updateUserReqValidator,
  UserSearchQueryValidator userSearchQueryValidator,
  IFileRepository fileRepository,
  ILogger<UserController> logger,
  IRedisClientFactory redisClientFactory
) : AtomiControllerBase
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

  [Authorize, HttpGet("{id}")]
  public async Task<ActionResult<UserRes>> GetById(string id)
  {
    var user = await this.GuardAsync(id)
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
      .Then(x => this.Guard(x?.Principal?.Id ?? "")
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
      Result<UserPrincipalRes> x = new Unauthorized("You are not authorized to access this resource").ToException();
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
    var sub = this.Sub();
    if (sub == null || sub != id)
    {
      Result<UserPrincipalRes> x = new Unauthorized("You are not authorized to access this resource").ToException();
      return this.ReturnResult(x);
    }

    var user = await updateUserReqValidator
      .ValidateAsyncResult(req, "Invalid UpdateUserReq")
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


  [HttpGet("redis-example/{key}")]
  public async Task<string> RedisExample(string key)
  {
    var redis = redisClientFactory.GetRedisClient(Caches.Main).Db0;
    return await redis.GetAsync<string>(key) ?? "Empty";
  }

  [HttpGet("redis-example/{key}/{value}")]
  public async Task<string> RedisExample(string key, string value)
  {
    var redis = redisClientFactory.GetRedisClient(Caches.Main).Db0;
    await redis.AddAsync(key, value);
    return await redis.GetAsync<string>(key) ?? "Empty";
  }

  [HttpPost("upload/{name}")]
  [Consumes(MediaTypeNames.Multipart.FormData)]
  public async Task<ActionResult<string>> Upload(string name, IFormFile file)
  {
    var redis = redisClientFactory.GetRedisClient(Caches.Main).Db0;

    if (file.Length <= 0) return "Empty";
    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    logger.LogInformation("Stream Size: {StreamSize}", stream.Length);

    var path = await fileRepository
      .Save(BlockStorages.Main, "sample", "sample", stream, true)
      .DoAwait(DoType.MapErrors, x => redis.AddAsync(name, x), Errors.MapAll)
      .ThenAwait(x => fileRepository.Link(BlockStorages.Main, x));
    return this.ReturnResult(path);
  }

  [HttpGet("download/{name}")]
  public async Task<ActionResult<string>> Download(string name)
  {
    var redis = redisClientFactory.GetRedisClient(Caches.Main).Db0;
    var path = await redis.GetAsync<string>(name);
    if (path == null) return "string";

    var r = await fileRepository.SignedLink(BlockStorages.Main, path, 60);
    return this.ReturnResult(r);
  }
}
