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

// the actual amount tin paid KTMB: strictly positive (a free ticket is not
// a purchase), in one of the two currencies the analysis knows how to cost
// (MYR converts via the FX queue, SGD passes through) — anything else would
// silently fall back to the estimate forever
public class CompleteKtmbCostReqValidator : AbstractValidator<CompleteKtmbCostReq>
{
  public CompleteKtmbCostReqValidator()
  {
    // an amount without a currency (or vice versa) is uncostable — both or
    // neither, like the other paired optional inputs
    this.RuleFor(x => x.KtmbCurrency)
      .Must((req, c) => (c == null) == (req.KtmbAmount == null))
      .WithMessage("ktmbAmount and ktmbCurrency must be provided together (or both omitted)");
    this.RuleFor(x => x.KtmbAmount)
      .GreaterThan(0)
      .When(x => x.KtmbAmount != null)
      .WithMessage("ktmbAmount must be greater than 0");
    // stay within the stored precision (numeric(16,8))
    this.RuleFor(x => x.KtmbAmount)
      .LessThanOrEqualTo(10_000_000)
      .When(x => x.KtmbAmount != null)
      .WithMessage("ktmbAmount must be at most 10000000");
    this.RuleFor(x => x.KtmbCurrency)
      .Must(c => c is null or "MYR" or "SGD")
      .WithMessage("ktmbCurrency must be 'MYR' or 'SGD'");
  }
}

public class SetBookingKtmbCostReqValidator : AbstractValidator<SetBookingKtmbCostReq>
{
  public SetBookingKtmbCostReqValidator()
  {
    this.RuleFor(x => x.Amount)
      .GreaterThan(0)
      .WithMessage("Amount must be greater than 0");
    // stay within the stored precision (numeric(16,8))
    this.RuleFor(x => x.Amount)
      .LessThanOrEqualTo(10_000_000)
      .WithMessage("Amount must be at most 10000000");
    this.RuleFor(x => x.Currency)
      .NotNull()
      .Must(c => c is "MYR" or "SGD")
      .WithMessage("Currency must be 'MYR' or 'SGD'");
  }
}

public class KtmbCostMissingQueryValidator : AbstractValidator<KtmbCostMissingQuery>
{
  public KtmbCostMissingQueryValidator()
  {
    this.RuleFor(x => x.Limit).Limit();
    this.RuleFor(x => x.Skip).Skip();
  }
}

public class SetKtmbFxRateReqValidator : AbstractValidator<SetKtmbFxRateReq>
{
  public SetKtmbFxRateReqValidator()
  {
    // SGD per 1 MYR: strictly positive (a zero rate would silently erase
    // every MYR cost), 100 as an obvious-typo ceiling
    this.RuleFor(x => x.Rate)
      .GreaterThan(0)
      .LessThanOrEqualTo(100)
      .WithMessage("Rate must be greater than 0 and at most 100");
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

public class PriorityEligibilityQueryValidator : AbstractValidator<PriorityEligibilityQuery>
{
  public PriorityEligibilityQueryValidator()
  {
    this.RuleFor(x => x.Direction)!.TrainDirectionValid();
    this.RuleFor(x => x.Date).NullableDateValid();
    this.RuleFor(x => x.Time).NullableTimeValid();
    // a partial slot is ambiguous (which timeslot?) — all three or none
    this.RuleFor(x => x.Time)
      .Must(
        (q, _) =>
          (q.Direction == null) == (q.Date == null) && (q.Date == null) == (q.Time == null)
      )
      .WithMessage("Direction, Date and Time must be provided together (or all omitted)");
  }
}

public class SetPrioritySettingsReqValidator : AbstractValidator<SetPrioritySettingsReq>
{
  public SetPrioritySettingsReqValidator()
  {
    this.RuleFor(x => x.Policies).NotNull();
    this.RuleForEach(x => x.Policies).SetValidator(new PriorityPolicyReqValidator());
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
    this.RuleFor(x => x.WindowStartSgt).NullableTimeValid();
    this.RuleFor(x => x.WindowEndSgt).NullableTimeValid();
    // a half-open window needs both bounds; a lone bound is ambiguous
    this.RuleFor(x => x.WindowEndSgt)
      .Must((req, x) => (x == null) == (req.WindowStartSgt == null))
      .WithMessage("WindowStartSgt and WindowEndSgt must be set together (or both omitted)");
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
    this.RuleFor(x => x.FeeKind)
      .Must(k => k is "Flat" or "Percent")
      .WithMessage("FeeKind must be 'Flat' or 'Percent'");
    this.RuleFor(x => x.FeeValue)
      .GreaterThanOrEqualTo(0)
      .WithMessage("FeeValue must be >= 0");
    this.RuleFor(x => x.FeeValue)
      .LessThanOrEqualTo(10_000)
      .When(x => x.FeeKind == "Flat")
      .WithMessage("A flat fee must be at most 10000");
    this.RuleFor(x => x.FeeValue)
      .LessThanOrEqualTo(100)
      .When(x => x.FeeKind == "Percent")
      .WithMessage("A percent fee must be at most 100");
    this.RuleFor(x => x.SlotCap)
      .GreaterThanOrEqualTo(1)
      .LessThanOrEqualTo(10_000)
      .When(x => x.SlotCap != null)
      .WithMessage("SlotCap must be between 1 and 10000");
    // fee/cap on a deny rule is dead configuration — surface the mistake
    this.RuleFor(x => x.FeeValue)
      .Must((req, fee) => req.Allow || fee == 0)
      .WithMessage("Only Allow rules may charge a fee");
    this.RuleFor(x => x.SlotCap)
      .Must((req, cap) => req.Allow || cap == null)
      .WithMessage("Only Allow rules may set a SlotCap");
  }
}
