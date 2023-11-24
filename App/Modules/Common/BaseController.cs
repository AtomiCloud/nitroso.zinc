using System.Net;
using App.Error;
using App.Error.V1;
using App.StartUp.Registry;
using App.Utility;
using CSharp_Result;
using Microsoft.AspNetCore.Mvc;

namespace App.Modules.Common;

public class AtomiControllerBase : ControllerBase
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

  protected Result<Unit> Guard(string target)
  {
    if (this.Sub() == target) return new Unit();
    return new Unauthorized("You are not authorized to access this resource").ToException();
  }

  protected Task<Result<Unit>> GuardAsync(string target)
  {
    if (this.Sub() == target) return new Unit().ToResult().ToAsyncResult();
    Result<Unit> r = new Unauthorized("You are not authorized to access this resource").ToException();
    return Task.FromResult(r);
  }

  protected string? Sub()
  {
    return this.HttpContext.User?.Identity?.Name;
  }
}
