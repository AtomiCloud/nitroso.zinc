using System.Net;
using System.Runtime.CompilerServices;
using App.Error;
using App.Error.V1;
using App.StartUp.Registry;
using App.StartUp.Services.Auth;
using App.Utility;
using CSharp_Result;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Common;

public class AtomiControllerBase(AuthHelper h) : ControllerBase
{
  protected ActionResult<T> Error<T>(HttpStatusCode code, IDomainProblem problem)
  {
    this.HttpContext.Items[Constants.ProblemContextKey] = problem;
    return this.StatusCode((int)code);
  }

  protected ActionResult Error(HttpStatusCode code, IDomainProblem problem)
  {
    this.HttpContext.Items[Constants.ProblemContextKey] = problem;
    return this.StatusCode((int)code);
  }


  // Error Mapping happens here
  private ActionResult MapException(Exception e)
  {
    return e switch
    {
      DomainProblemException d => d.Problem switch
      {
        EntityNotFound => this.Error(HttpStatusCode.NotFound, d.Problem),
        UnknownFileType unknownFileType => this.Error(HttpStatusCode.NotAcceptable, unknownFileType),
        ValidationError validationError => this.Error(HttpStatusCode.BadRequest, validationError),
        Unauthorized unauthorizedError => this.Error(HttpStatusCode.Unauthorized, unauthorizedError),
        EntityConflict entityConflict => this.Error(HttpStatusCode.Conflict, entityConflict),
        MultipleEntityNotFound multipleEntityNotFound => this.Error(HttpStatusCode.NotFound, multipleEntityNotFound),
        _ => this.Error(HttpStatusCode.BadRequest, d.Problem),
      },
      _ => throw new AggregateException("Unhandled Exception", e),
    };
  }

  private ActionResult<T> MapException<T>(Exception e)
  {
    return this.MapException(e);
  }

  protected ActionResult ReturnUnitNullableResult(Result<Unit?> ent, EntityNotFound notFound)
  {
    if (ent.IsSuccess())
    {
      var suc = ent.Get();
      return suc == null ? this.Error(HttpStatusCode.NotFound, notFound) : this.NoContent();
    }

    var e = ent.FailureOrDefault();
    return this.MapException(e);
  }

  protected ActionResult ReturnUnitResult(Result<Unit> ent)
  {
    if (ent.IsSuccess()) return this.NoContent();
    var e = ent.FailureOrDefault();
    return this.MapException(e);
  }

  protected ActionResult<T> ReturnNullableResult<T>(Result<T?> entity, EntityNotFound notFound)
  {
    if (entity.IsSuccess())
    {
      var suc = entity.Get();
      return suc == null ? this.Error<T>(HttpStatusCode.NotFound, notFound) : this.Ok(suc);
    }

    var e = entity.FailureOrDefault();
    return this.MapException<T>(e);
  }

  protected ActionResult<T> ReturnResult<T>(Result<T> entity)
  {
    return entity.IsSuccess()
      ? this.Ok(entity.Get())
      : this.MapException<T>(entity.FailureOrDefault());
  }

  protected Result<Unit> Guard(string? target)
  {
    if (target != null && this.Sub() == target) return new Unit();
    return new Unauthorized("You are not authorized to access this resource").ToException();
  }

  protected Task<Result<Unit>> GuardAsync(string? target)
  {
    return Task.FromResult(this.Guard(target));
  }

  protected Result<Unit> GuardOrAll(string? target, string field, params string[] value)
  {
    if (
      (target != null && this.Sub() == target)
      ||
      h.HasAll(this.HttpContext.User, field, value)
    ) return new Unit().ToResult();
    return new Unauthorized("You are not authorized to access this resource").ToException();
  }

  protected Task<Result<Unit>> GuardOrAllAsync(string? target, string field, params string[] value)
  {
    return Task.FromResult(this.GuardOrAll(target, field, value));
  }

  protected Result<Unit> GuardOrAny(string? target, string field, params string[] value)
  {
    if (
      (target != null && this.Sub() == target)
      ||
      h.HasAny(this.HttpContext.User, field, value)
    ) return new Unit().ToResult();
    return new Unauthorized("You are not authorized to access this resource").ToException();
  }

  protected Task<Result<Unit>> GuardOrAnyAsync(string? target, string field, params string[] value)
  {
    return Task.FromResult(this.GuardOrAny(target, field, value));
  }


  protected ActionResult<T> BlockAuth<T>()
  {
    Result<T> r = new Unauthorized("You are not authorized to access this resource").ToException();
    return this.ReturnResult(r);
  }

  protected Task<ActionResult<T>> BlockAuthAsync<T>()
  {
    return Task.FromResult(this.BlockAuth<T>());
  }

  protected string? Sub() => this.HttpContext.User?.Identity?.Name;
}
