using App.Utility;
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
