using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using App.Modules.Common;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[Description(
  "Like conflict error occurs when a user tries to like a template, plugin or processor that they have already liked, or unlike a template, plugin or processor that they have not liked."
)]
internal class LikeConflictError : IDomainProblem
{
  public LikeConflictError() { }

  public LikeConflictError(
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
  public string Id { get; } = "like_conflict";

  [JsonIgnore, JsonSchemaIgnore]
  public string Title { get; } = "Like Conflict";

  [JsonIgnore, JsonSchemaIgnore]
  public string Version { get; } = "v1";

  [JsonIgnore, JsonSchemaIgnore]
  public string Detail { get; } = string.Empty;

  [Description(
    "Type of Resource that like conflicted. Can be either 'template', 'plugin' or 'processor'"
  )]
  public string ResourceType { get; } = string.Empty;

  [Description("ID of the resource that like conflicted")]
  public string ResourceId { get; } = string.Empty;

  [Description("Conflict type of the like. Can be either 'like' or 'unlike'")]
  public string ConflictType { get; } = string.Empty;
}
