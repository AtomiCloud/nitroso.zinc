using System.Reflection;
using System.Text;
using CSharp_Result;
using HandlebarsDotNet;

namespace App.StartUp.Email;

public class EmailRenderer(
  ILogger<EmailRenderer> logger
) : IEmailRenderer
{
  private readonly Dictionary<string, HandlebarsTemplate<object, object>> _templateCache = [];


  public async Task<Result<HandlebarsTemplate<object, object>>> GetTemplate(string id)
  {
    if (this._templateCache.TryGetValue(id, out var value)) return value;
    return await
      LoadEmbeddedResourceAsync($"App.Templates.Email.templates.{id}.html")
        .Then(Handlebars.Compile, Errors.MapNone)
        .Do(DoType.Ignore, x => this._templateCache[id] = x, Errors.MapNone);
  }

  public async Task<Result<string>> RenderEmail(string id, object variables)
  {
    return await this.GetTemplate(id).Then(x => x(variables), Errors.MapNone);
  }


  private async Task<Result<string>> LoadEmbeddedResourceAsync(string resourceName)
  {
    var assembly = typeof(EmailRenderer).GetTypeInfo().Assembly;

    await using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null)
    {
      var ex = new FileNotFoundException($"Embedded resource not found: {resourceName}");
      logger.LogError(ex, "Failed to load email templates from embedded assembly");
      return ex;
    }


    using var reader = new StreamReader(stream, Encoding.UTF8);
    return await reader.ReadToEndAsync();
  }
}
