using App.Utility;
using FluentValidation;

namespace App.Modules.Bookings.API.V1;

public class BookingPassengerReqValidator : AbstractValidator<BookingPassengerReq>
{
  public BookingPassengerReqValidator()
  {
    this.RuleFor(x => x.FullName)
      .NotNull()
      .MaximumLength(512);
    this.RuleFor(x => x.Gender)
      .NotNull()
      .GenderValid();

    this.RuleFor(x => x.PassportExpiry)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.PassportNumber)
      .NotNull()
      .MaximumLength(64);
  }
}

public class CreateBookingReqValidator : AbstractValidator<CreateBookingReq>
{
  public CreateBookingReqValidator()
  {
    this.RuleFor(x => x.Date)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.Time)
      .NotNull()
      .TimeValid();
    this.RuleFor(x => x.Direction)
      .NotNull()
      .TrainDirectionValid();
    this.RuleFor(x => x.Passenger)
      .NotNull()
      .SetValidator(new BookingPassengerReqValidator());
  }
}

public class UpdateBookingReqValidator : AbstractValidator<UpdateBookingReq>
{
  public UpdateBookingReqValidator()
  {
    this.RuleFor(x => x.Date)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.Time)
      .NotNull()
      .TimeValid();
    this.RuleFor(x => x.Direction)
      .NotNull()
      .TrainDirectionValid();
    this.RuleFor(x => x.Passenger)
      .NotNull()
      .SetValidator(new BookingPassengerReqValidator());
  }
}

public class BookingSearchQueryValidator : AbstractValidator<SearchBookingQuery>
{
  public BookingSearchQueryValidator()
  {
    this.RuleFor(x => x.Date)
      .NullableDateValid();
    this.RuleFor(x => x.Time)
      .NullableTimeValid();
    this.RuleFor(x => x.Direction)!
      .TrainDirectionValid();
    this.RuleFor(x => x.Limit)
      .Limit();
    this.RuleFor(x => x.Skip)
      .Skip();
  }
}


public class BookingCountQueryValidator : AbstractValidator<BookingCountQuery>
{
  public BookingCountQueryValidator()
  {
    this.RuleFor(x => x.Date)
      .NullableDateValid();
    this.RuleFor(x => x.Direction)!
      .TrainDirectionValid();
  }
}

public class ReserveBookingQueryValidator : AbstractValidator<ReserveBookingQuery>
{
  public ReserveBookingQueryValidator()
  {
    this.RuleFor(x => x.Date)
      .DateValid();
    this.RuleFor(x => x.Time)
      .TimeValid();
    this.RuleFor(x => x.Direction)
      .NotNull()
      .TrainDirectionValid();
  }
}
