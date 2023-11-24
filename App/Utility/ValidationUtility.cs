using App.Error.V1;
using CSharp_Result;
using FluentValidation;
using Humanizer;

namespace App.Utility;

public static class ValidationUtility
{
  public static async Task<Result<T>> ValidateAsyncResult<T>(this AbstractValidator<T> validator, T obj, string errorMessage)
  {
    var result = await validator.ValidateAsync(obj);
    return result.IsValid
      ? obj.ToResult()
      : new ValidationError(errorMessage, result.ToDictionary()).ToException();
  }


  public static Result<T> ValidateResult<T>(this AbstractValidator<T> validator, T obj, string errorMessage)
  {
    var result = validator.Validate(obj);
    return result.IsValid
      ? obj.ToResult()
      : new ValidationError(errorMessage, result.ToDictionary()).ToException();
  }

  public static IRuleBuilderOptions<T, int?> Limit<T>(
    this IRuleBuilder<T, int?> ruleBuilder)
  {
    return ruleBuilder
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(100)
      .WithMessage("Limit has to be between 0 to 100 characters");
  }

  public static IRuleBuilderOptions<T, int?> Skip<T>(
    this IRuleBuilder<T, int?> ruleBuilder)
  {
    return ruleBuilder
      .GreaterThanOrEqualTo(0)
      .WithMessage("Skip has to be larger than or equal to 0");
  }

  public static IRuleBuilderOptions<T, string> ValidEnum<T>(
    this IRuleBuilder<T, string> ruleBuilder, string[] validValues)
  {
    return ruleBuilder
      .Must(validValues.Contains)
      .WithMessage("Must be one of the following: " + validValues.Humanize("or"));
  }


  public static IRuleBuilderOptions<T, string> DateValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Must(x => DateOnly.TryParseExact(x, Utils.StandardDateFormat, out _))
      .WithMessage("DateOnly must be in the format of dd-mm-yyyy");
  }

  public static IRuleBuilderOptions<T, string> TagValid<T>(this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Length(1, 32)
      .WithMessage("Docker tag must be between 1 and 32 characters")
      .Matches(@"^[a-z0-9](\-?[a-z0-9]+)*$")
      .WithMessage("Docker tag can only contain alphanumeric characters and dashes, and cannot star or end with dash");
  }

  public static IRuleBuilderOptions<T, string> UsernameValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Length(1, 256)
      .WithMessage("Username has to be between 1 to 256 characters")
      .Matches(@"^[a-z](\-?[a-z0-9]+)*$")
      .WithMessage("Username can only contain alphanumeric characters and dashes, and cannot end or start with dashes or numbers");
  }

  public static IRuleBuilderOptions<T, string> ShaValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Matches("^[0-9a-fA-F]{64}$")
      .WithMessage("SHA can only have hexadecimal characters and exactly 64");
  }

  public static IRuleBuilderOptions<T, string> DockerReferenceValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Matches(@"^((\w(-?\w+)*)(\.\w(-?\w+)*)*(:\d+)?/)?\w(-?\w+)*(/\w(-?\w+)*)*$")
      .WithMessage("Invalid Docker reference");
  }


  public static IRuleBuilderOptions<T, string> NameValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Length(1, 256)
      .WithMessage("Name has to be between 1 to 256 characters");
  }


  public static IRuleBuilderOptions<T, string> DescriptionValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Length(2, 2048)
      .WithMessage("Description has to be between 2 to 2048 characters");
  }

  public static IRuleBuilderOptions<T, string> UrlValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .Must(x =>
      {
        try
        {
          var _ = new Uri(x);
          return true;
        }
        catch
        {
          return false;
        }
      })
      .WithMessage("Url is invalid");
  }

  public static IRuleBuilderOptions<T, IEnumerable<TElement>> MaxCollectionLength<T, TElement>(
    this IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder, int max)
  {
    return ruleBuilder
      .Must(x => x.Count() <= max);
  }


  public static IRuleBuilderOptions<T, IEnumerable<TElement>> MinCollectionLength<T, TElement>(
    this IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder, int min)
  {
    return ruleBuilder
      .Must(x => x.Count() >= min);
  }

  public static IRuleBuilderOptions<T, IEnumerable<TElement>> CollectionLength<T, TElement>(
    this IRuleBuilder<T, IEnumerable<TElement>> ruleBuilder, int min, int max)
  {
    return ruleBuilder
      .Must(x =>
      {
        var tElements = x as TElement[] ?? x.ToArray();
        return tElements.Length >= min && tElements.Length <= max;
      });
  }


  public static IRuleBuilder<T, string> SearchValid<T>(
    this IRuleBuilder<T, string> ruleBuilder)
  {
    return ruleBuilder
      .MinimumLength(1)
      .WithMessage("Search term needs to have at least 1 character");
  }
}
