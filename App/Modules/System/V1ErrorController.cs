using System.Net;
using System.Reflection;
using System.Text.Json;
using App.Error;
using App.Error.V1;
using App.Modules.Common;
using App.StartUp.Services.Auth;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using NJsonSchema;

namespace App.Modules.System;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/error-info")]
public class V1ErrorController(AuthHelper h) : AtomiControllerBase(h)
{
  private static readonly IEnumerable<Type> V1ProblemTypes =
    from t in Assembly.GetExecutingAssembly().GetTypes()
    where t.IsClass && t.Namespace == "App.Error.V1"
    select t;

  [HttpGet]
  public ActionResult<IEnumerable<string>> ErrorInfo()
  {
    return this.Ok(
      V1ProblemTypes
        .Select(x => (IDomainProblem)Activator.CreateInstance(x)!)
        .Select(x => x.Id)
    );
  }

  [HttpGet("{id}")]
  public ActionResult<ErrorInfo> Get(string id)
  {
    var problem = V1ProblemTypes
      .Select(x => (x, (IDomainProblem?)Activator.CreateInstance(x)))
      .Where(x => x.Item2?.Id == id)
      .Select(x => x.Item2)
      .FirstOrDefault();


    if (problem == null)
    {
      return this.Error<ErrorInfo>(HttpStatusCode.NotFound,
        new EntityNotFound("The IDomainProblem does not exist", typeof(IDomainProblem), id));
    }

    var json = JsonSchema.CreateAnySchema();

    var schema = JsonSchema.FromType(problem.GetType()).ActualSchema.ToJson() ?? "{}";

    var s = JsonSerializer.Deserialize<object>(schema,
      new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return this.Ok(new ErrorInfo(s!, problem.Id, problem.Title, problem.Version));
  }
}
