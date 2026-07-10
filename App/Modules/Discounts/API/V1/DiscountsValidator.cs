using App.Utility;
using FluentValidation;

namespace App.Modules.Discounts.API.V1;

public class DiscountSearchQueryValidator : AbstractValidator<DiscountSearchQuery>
{
  public DiscountSearchQueryValidator()
  {
    this.RuleFor(x => x.Search)
      .Length(1, 256)
      .WithMessage("Name has to be between 1 to 256 characters");
    this.RuleFor(x => x.DiscountType).DiscountTypeValid();
    this.RuleFor(x => x.MatchMode).DiscountMatchModeValid();
    this.RuleForEach(x => x.MatchTarget).MaximumLength(255);
  }
}

public class DiscountMatchReqValidator : AbstractValidator<DiscountMatchReq>
{
  public DiscountMatchReqValidator()
  {
    this.RuleFor(x => x.Value)
      .NotNull()
      .Length(1, 256)
      .WithMessage("Value has to be between 1 to 256 characters");
    this.RuleFor(x => x.MatchType).NotNull().DiscountMatchTypeValid();
  }
}

public class DiscountTargetReqValidator : AbstractValidator<DiscountTargetReq>
{
  public DiscountTargetReqValidator()
  {
    this.RuleFor(x => x.MatchMode).NotNull().DiscountMatchModeValid();
    this.RuleForEach(x => x.Matches).NotNull().SetValidator(new DiscountMatchReqValidator());
  }
}

public class DiscountRecordReqValidator : AbstractValidator<DiscountRecordReq>
{
  public DiscountRecordReqValidator()
  {
    this.RuleFor(x => x.Name)
      .NotNull()
      .Length(1, 256)
      .WithMessage("Name has to be between 1 to 256 characters");
    this.RuleFor(x => x.Description)
      .NotNull()
      .Length(1, 2048)
      .WithMessage("Description has to be between 1 to 2048 characters");
    this.RuleFor(x => x.Amount).NotNull().GreaterThanOrEqualTo(0);
    this.RuleFor(x => x.Type).NotNull().DiscountTypeValid();
    // slot matchers mirror the cost policy API (null = wildcard)
    this.RuleFor(x => x.MatchDate).NullableDateValid();
    this.RuleFor(x => x.MatchTime).NullableTimeValid();
    // Enum.TryParse would happily accept numerics ("9") — names only
    this.RuleFor(x => x.MatchDayOfWeek)
      .Must(x => x is null || Enum.GetNames<DayOfWeek>().Contains(x))
      .WithMessage(
        "MatchDayOfWeek must be one of: Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday"
      );
    this.RuleFor(x => x.MatchDirection)!.TrainDirectionValid();
    this.RuleFor(x => x.LeadTimeAtLeastHours)
      .GreaterThanOrEqualTo(1)
      .LessThanOrEqualTo(8760)
      .When(x => x.LeadTimeAtLeastHours != null)
      .WithMessage("LeadTimeAtLeastHours must be between 1 and 8760 (a year)");
    this.RuleFor(x => x.LeadTimeUnderHours)
      .GreaterThanOrEqualTo(1)
      .LessThanOrEqualTo(8760)
      .When(x => x.LeadTimeUnderHours != null)
      .WithMessage("LeadTimeUnderHours must be between 1 and 8760 (a year)");
    this.RuleFor(x => x)
      .Must(LeadTimeFieldsAgree)
      .WithMessage("LeadTimeAtLeastHours and deprecated LeadTimeUnderHours must match");
    // compare NORMALIZED instants: JSON can mix kinds (Z suffix = Utc,
    // offset = Local, bare = Unspecified-as-UTC) and comparing the raw
    // values would let an inverted — silently dead — window through
    this.RuleFor(x => x.ExpiresAt)
      .Must(
        (req, x) =>
          x == null || req.EffectiveAt == null || ToUtc(x.Value) > ToUtc(req.EffectiveAt.Value)
      )
      .WithMessage("ExpiresAt must be after EffectiveAt");
  }

  private static DateTime ToUtc(DateTime at) =>
    at.Kind switch
    {
      DateTimeKind.Utc => at,
      DateTimeKind.Local => at.ToUniversalTime(),
      _ => DateTime.SpecifyKind(at, DateTimeKind.Utc),
    };

  public static bool LeadTimeFieldsAgree(DiscountRecordReq request) =>
    request.LeadTimeAtLeastHours == null
    || request.LeadTimeUnderHours == null
    || request.LeadTimeAtLeastHours == request.LeadTimeUnderHours;
}

public class DiscountStatusReqValidator : AbstractValidator<DiscountStatusReq>
{
  public DiscountStatusReqValidator()
  {
    this.RuleFor(x => x.Disabled).NotNull();
  }
}

public class CreateDiscountReqValidator : AbstractValidator<CreateDiscountReq>
{
  public CreateDiscountReqValidator()
  {
    this.RuleFor(x => x.Target).NotNull().SetValidator(new DiscountTargetReqValidator());
    this.RuleFor(x => x.Record).NotNull().SetValidator(new DiscountRecordReqValidator());
  }
}

public class UpdateDiscountReqValidator : AbstractValidator<UpdateDiscountReq>
{
  public UpdateDiscountReqValidator()
  {
    this.RuleFor(x => x.Target).NotNull().SetValidator(new DiscountTargetReqValidator());
    this.RuleFor(x => x.Record).NotNull().SetValidator(new DiscountRecordReqValidator());
    this.RuleFor(x => x.Status).NotNull().SetValidator(new DiscountStatusReqValidator());
  }
}
