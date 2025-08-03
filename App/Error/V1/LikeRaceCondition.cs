using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using App.Modules.Common;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description(
  "Like race condition error occurs when a user tries to like a template, plugin or processor, when the start of the like query, the like exist and at the end of the like query, the like disappeared."
)]
internal class LikeRaceConditionError : IDomainProblem
{
  public LikeRaceConditionError() { }

  public LikeRaceConditionError(
    string detail,
    string resourceId,
    string resourceType,
    string conflictType
  )
  {
    this.Detail = detail;
    this.ResourceId = resourceId;
    this.ResourceType = resourceType;
    this.ConflictType = conflictType;
  }

  [JsonIgnore, JsonSchemaIgnore]
  public string Id { get; } = "like_race_condition";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "Like Race Condition";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;

  [Description(
    "Type of Resource that like have race condition. Can be either 'template', 'plugin' or 'processor'"
  )]
  public string ResourceType { get; } = string.Empty;

  [Description("ID of the resource that like have race condition")]
  public string ResourceId { get; } = string.Empty;

  [Description("Type of the like of the race condition. Can be either 'like' or 'unlike'")]
  public string ConflictType { get; } = string.Empty;
}
