using System.Text.Json.Serialization;
using NJsonSchema.Annotations;

namespace App.Error;

public interface IDomainProblem
{
  [JsonIgnore, JsonSchemaIgnore] internal string Id { get; }

  [JsonIgnore, JsonSchemaIgnore] internal string Title { get; }

  [JsonIgnore, JsonSchemaIgnore] internal string Detail { get; }

  [JsonIgnore, JsonSchemaIgnore] internal string Version { get; }
}
