namespace App.StartUp.Services.Swagger;

public static class SwaggerService
{
  public static WebApplication UseSwaggerService(this WebApplication app)
  {
    app.UseSwagger();
    app.UseSwaggerUI(
      options =>
      {
        // build a swagger endpoint for each discovered API version
        var descriptions = app.DescribeApiVersions();

        // build a swagger endpoint for each discovered API version
        foreach (var description in descriptions)
        {
          var url = $"/swagger/{description.GroupName}/swagger.json";
          var name = description.GroupName.ToUpperInvariant();
          options.SwaggerEndpoint(url, name);
        }
      }
    );
    return app;
  }
}
