using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using App.Modules.Common;
using NJsonSchema.Annotations;

namespace App.Error.V1;

[
  Description(
    "This error occurs when the request has simple valdiation errors, such as format, length, type, etc. Errors are emitted and validated by FluentValidation")
]
internal class ValidationError : IDomainProblem
{
  public ValidationError() { }

  public ValidationError(string detail, IDictionary<string, string[]> errors)
  {
    this.Detail = detail;
    this.Errors = errors;
  }

  [JsonIgnore] public string Id { get; } = "validation_error";
  [JsonIgnore] public string Title { get; } = "Validation Error";
  [JsonIgnore] public string Version { get; } = "v1";
  [JsonIgnore] public string Detail { get; } = string.Empty;

  [Description(
    "The errors that occured during validation. Keys are the fields that failed validation, values are the error messages")]
  public IDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>();
}
