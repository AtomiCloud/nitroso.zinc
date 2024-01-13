using System.Net;
using App.Error;
using App.Error.V1;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace App.StartUp.Services.Auth;

public class AuthorizationResultTransformer(IOptions<ErrorPortalOption> ep, IOptions<AppOption> ap) : IAuthorizationMiddlewareResultHandler
{
  private readonly IAuthorizationMiddlewareResultHandler _handler = new AuthorizationMiddlewareResultHandler();

  public Task HandleAsync(
    RequestDelegate requestDelegate,
    HttpContext httpContext,
    AuthorizationPolicy authorizationPolicy,
    PolicyAuthorizationResult policyAuthorizationResult)
  {
    if (policyAuthorizationResult is { Forbidden: true, AuthorizationFailure: not null })
    {
      if (policyAuthorizationResult.AuthorizationFailure.FailedRequirements.Any(requirement =>
            requirement is HasAnyRequirement or HasAllRequirement))
      {

        if (httpContext.Items[Constants.ProblemContextKey] is IDomainProblem problem)
        {
          var p = new ProblemDetails
          {
            Detail = problem.Detail,
            Title = problem.Title,
            Extensions = { ["data"] = problem }
          };
          if (ep.Value.Enabled)
          {
            p.Type =
              $"{ep.Value.Scheme}://{ep.Value.Host}/docs/{ap.Value.Landscape}/{ap.Value.Platform}/{ap.Value.Service}/{ap.Value.Module}/{problem.Version}/{problem.Id}";
          }
          p.Status = (int)HttpStatusCode.Forbidden;
          p.Extensions.Add(new KeyValuePair<string, object?>("traceId", httpContext.TraceIdentifier));
          var result = new ObjectResult(p)
          {
            ContentTypes = [],
            StatusCode = p.Status,
            DeclaredType = p.GetType(),
          };
          var executor = httpContext.RequestServices.GetRequiredService<IActionResultExecutor<ObjectResult>>();
          var routeData = httpContext.GetRouteData() ?? new RouteData();
          var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
          return executor.ExecuteAsync(actionContext, result);
        };
      }
    }

    return this._handler.HandleAsync(requestDelegate, httpContext, authorizationPolicy, policyAuthorizationResult);
  }
}
