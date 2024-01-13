using App.Error;
using App.Modules.Common;
using App.StartUp.Options;
using App.StartUp.Registry;
using App.Utility;

namespace App.StartUp.Services;

public static class ProblemDetailsService
{
  public static IServiceCollection AddProblemDetailsService(this IServiceCollection service, ErrorPortalOption ep,
    AppOption ap)
  {
    service.AddProblemDetails(x => x.CustomizeProblemDetails =
      context =>
      {
        Console.WriteLine(context.HttpContext.Items[Constants.ProblemContextKey]?.ToJson() ?? "null");
        if (context.HttpContext.Items[Constants.ProblemContextKey] is not IDomainProblem problem) return;
        context.ProblemDetails.Detail = problem.Detail;
        context.ProblemDetails.Title = problem.Title;
        if (ep.Enabled)
        {
          context.ProblemDetails.Type =
            $"{ep.Scheme}://{ep.Host}/docs/{ap.Landscape}/{ap.Platform}/{ap.Service}/{ap.Module}/{problem.Version}/{problem.Id}";
        }
        context.ProblemDetails.Extensions["data"] = problem;
        Console.WriteLine("here====================");
      });
    return service;
  }
}
