using App.Modules.Discounts.API.V1;
using App.Utility;
using Domain.Booking;
using FluentValidation;

namespace App.Modules.Bookings.API.V1;

public class BookingPassengerReqValidator : AbstractValidator<BookingPassengerReq>
{
  public BookingPassengerReqValidator()
  {
    this.RuleFor(x => x.FullName).NotNull().MaximumLength(512).Matches("^[a-zA-Z @./',\\-`*]+$");
    this.RuleFor(x => x.Gender).NotNull().GenderValid();

    this.RuleFor(x => x.PassportExpiry).NotNull().DateValid();
    this.RuleFor(x => x.PassportNumber).NotNull().MaximumLength(20).Matches("^([a-zA-Z0-9]+)$");
  }
}

public class CreateBookingReqValidator : AbstractValidator<CreateBookingReq>
{
  public CreateBookingReqValidator()
  {
    this.RuleFor(x => x.Date).NotNull().DateValid();
    this.RuleFor(x => x.Time).NotNull().TimeValid();
    this.RuleFor(x => x.Direction).NotNull().TrainDirectionValid();
    this.RuleFor(x => x.Passenger).NotNull().SetValidator(new BookingPassengerReqValidator());
    // null = legacy (raichu) argon client without the quote flow; when the
    // quote is present it must be the canonical token Cost/summary returned
    this.RuleFor(x => x.ExpectedCost)
      .Must(x => x == null || BookingPriceQuote.IsCanonical(x))
      .WithMessage("ExpectedCost must be a canonical non-negative decimal quote when provided");
  }
}

public class UpdateBookingReqValidator : AbstractValidator<UpdateBookingReq>
{
  public UpdateBookingReqValidator()
  {
    this.RuleFor(x => x.Date).NotNull().DateValid();
    this.RuleFor(x => x.Time).NotNull().TimeValid();
    this.RuleFor(x => x.Direction).NotNull().TrainDirectionValid();
    this.RuleFor(x => x.Passenger).NotNull().SetValidator(new BookingPassengerReqValidator());
  }
}

public class BookingSearchQueryValidator : AbstractValidator<SearchBookingQuery>
{
  public BookingSearchQueryValidator()
  {
    this.RuleFor(x => x.Date).NullableDateValid();
    this.RuleFor(x => x.Time).NullableTimeValid();
    this.RuleFor(x => x.Direction)!.TrainDirectionValid();
    this.RuleFor(x => x.PassportNumber)
      .MaximumLength(20)
      .Matches("^([a-zA-Z0-9]+)$")
      .When(x => x.PassportNumber != null);
    // fuzzy name filter: same alphabet passenger names are stored in, plus
    // nothing that could not appear in one
    this.RuleFor(x => x.PassengerName)
      .MaximumLength(512)
      .Matches("^[a-zA-Z @./',\\-`*]+$")
      .When(x => x.PassengerName != null);
    // upper bound keeps DateTime.UtcNow.AddMinutes(-x) in range (a 500 otherwise)
    this.RuleFor(x => x.StuckForMinutes)
      .GreaterThanOrEqualTo(1)
      .LessThanOrEqualTo(525600)
      .When(x => x.StuckForMinutes != null);
    // MissingTicket implies Completed; any other explicit Status filter can
    // only ever produce an empty page, so reject the contradiction upfront
    this.RuleFor(x => x.Status)
      .Must(x => x is null or "Completed")
      .WithMessage("MissingTicket=true only applies to Completed bookings")
      .When(x => x.MissingTicket == true);
    this.RuleFor(x => x.SortBy)
      .Must(x => x is "Timing" or "PassengerName" or "PassportNumber" or "BuyTime" or "FulfilTime")
      .WithMessage(
        "SortBy must be one of: Timing, PassengerName, PassportNumber, BuyTime, FulfilTime"
      )
      .When(x => x.SortBy != null);
    this.RuleFor(x => x.Limit).Limit();
    this.RuleFor(x => x.Skip).Skip();
  }
}

public class BookingStatsQueryReqValidator : AbstractValidator<BookingStatsQueryReq>
{
  public BookingStatsQueryReqValidator()
  {
    this.RuleFor(x => x.After).NullableDateValid();
    this.RuleFor(x => x.Before).NullableDateValid();
  }
}

public class BookingAnalysisQueryReqValidator : AbstractValidator<BookingAnalysisQueryReq>
{
  public BookingAnalysisQueryReqValidator()
  {
    this.RuleFor(x => x.After).NullableDateValid();
    this.RuleFor(x => x.Before).NullableDateValid();
  }
}

public class BookingBoostQueryReqValidator : AbstractValidator<BookingBoostQueryReq>
{
  public BookingBoostQueryReqValidator()
  {
    this.RuleFor(x => x.After).NullableDateValid();
    this.RuleFor(x => x.Before).NullableDateValid();
    this.RuleFor(x => x.Limit)
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(200)
      .WithMessage("Limit has to be between 0 and 200");
    this.RuleFor(x => x.Skip).Skip();
  }
}

public class SetKtmbCostReqValidator : AbstractValidator<SetKtmbCostReq>
{
  public SetKtmbCostReqValidator()
  {
    this.RuleFor(x => x.Direction).NotNull().TrainDirectionValid();
    this.RuleFor(x => x.Cost)
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(10_000)
      .WithMessage("Cost must be between 0 and 10000");
    // a past effective date would insert a row that can never win the
    // newest-effective ordering — a silently dead change (small tolerance
    // for clock skew; omit the field for an immediate change)
    this.RuleFor(x => x.EffectiveAt)
      .Must(x => x == null || x > DateTime.UtcNow.AddMinutes(-5))
      .WithMessage("EffectiveAt must be in the future (omit it for an immediate change)");
  }
}

public class BookingCountQueryValidator : AbstractValidator<BookingCountQuery>
{
  public BookingCountQueryValidator()
  {
    this.RuleFor(x => x.Date).NullableDateValid();
    this.RuleFor(x => x.Direction)!.TrainDirectionValid();
  }
}

public class ReserveBookingQueryValidator : AbstractValidator<ReserveBookingQuery>
{
  public ReserveBookingQueryValidator()
  {
    this.RuleFor(x => x.Date).DateValid();
    this.RuleFor(x => x.Time).TimeValid();
    this.RuleFor(x => x.Direction).NotNull().TrainDirectionValid();
  }
}

public class SetPrioritySettingsReqValidator : AbstractValidator<SetPrioritySettingsReq>
{
  public SetPrioritySettingsReqValidator()
  {
    this.RuleFor(x => x.Fee)
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(10_000)
      .WithMessage("Fee must be between 0 and 10000");
    this.RuleFor(x => x.WindowStartSgt).NullableTimeValid();
    this.RuleFor(x => x.WindowEndSgt).NullableTimeValid();
    // a half-open window needs both bounds; a lone bound is ambiguous
    this.RuleFor(x => x.WindowEndSgt)
      .Must((req, x) => (x == null) == (req.WindowStartSgt == null))
      .WithMessage("WindowStartSgt and WindowEndSgt must be set together (or both omitted)");
    // targets reuse the Discounts API shapes and validation; null = omitted
    this.RuleFor(x => x.FreeTarget)!
      .SetValidator(new DiscountTargetReqValidator()!)
      .When(x => x.FreeTarget != null);
    this.RuleFor(x => x.AccessTarget)!
      .SetValidator(new DiscountTargetReqValidator()!)
      .When(x => x.AccessTarget != null);
    this.RuleFor(x => x.SlotCap)
      .GreaterThanOrEqualTo(1)
      .LessThanOrEqualTo(10_000)
      .When(x => x.SlotCap != null)
      .WithMessage("SlotCap must be between 1 and 10000");
    this.RuleForEach(x => x.Policies)
      .SetValidator(new PriorityPolicyReqValidator())
      .When(x => x.Policies != null);
  }
}

public class PriorityPolicyReqValidator : AbstractValidator<PriorityPolicyReq>
{
  public PriorityPolicyReqValidator()
  {
    this.RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    this.RuleFor(x => x.Target)!
      .SetValidator(new DiscountTargetReqValidator()!)
      .When(x => x.Target != null);
    this.RuleFor(x => x.MinHoursToDeparture)
      .GreaterThanOrEqualTo(0)
      .When(x => x.MinHoursToDeparture != null)
      .WithMessage("MinHoursToDeparture must be >= 0");
    this.RuleFor(x => x.MaxHoursToDeparture)
      .GreaterThan(0)
      .When(x => x.MaxHoursToDeparture != null)
      .WithMessage("MaxHoursToDeparture must be > 0");
    // an empty interval can never apply — reject the typo
    this.RuleFor(x => x.MaxHoursToDeparture)
      .Must(
        (req, max) => max == null || req.MinHoursToDeparture == null || max > req.MinHoursToDeparture
      )
      .WithMessage("MaxHoursToDeparture must be greater than MinHoursToDeparture");
    this.RuleFor(x => x.FeeOverride)
      .GreaterThanOrEqualTo(0)
      .LessThanOrEqualTo(10_000)
      .When(x => x.FeeOverride != null)
      .WithMessage("FeeOverride must be between 0 and 10000");
    // FeeOverride on a deny rule is dead configuration — surface the mistake
    this.RuleFor(x => x.FeeOverride)
      .Must((req, fee) => fee == null || req.Allow)
      .WithMessage("FeeOverride only applies to Allow rules");
  }
}
