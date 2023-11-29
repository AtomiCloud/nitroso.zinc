using App.Utility;
using FluentValidation;

namespace App.Modules.Bookings.API.V1;

public class BookingPassengerReqValidator : AbstractValidator<BookingPassengerReq>
{
  public BookingPassengerReqValidator()
  {
    this.RuleFor(x => x.FullName)
      .NotNull();
    this.RuleFor(x => x.Gender)
      .NotNull()
      .GenderValid();

    this.RuleFor(x => x.PassportExpiry)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.PassportNumber)
      .NotNull();
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
    this.RuleFor(x => x.Passengers)
      .NotNull();
    this.RuleForEach(x => x.Passengers)
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
    this.RuleFor(x => x.Passengers)
      .NotNull();
    this.RuleForEach(x => x.Passengers)
      .NotNull()
      .SetValidator(new BookingPassengerReqValidator());
  }
}

public class BookingSearchQueryValidator : AbstractValidator<SearchBookingQuery>
{
  public BookingSearchQueryValidator()
  {
    this.RuleFor(x => x.Date)
      .DateValid();
    this.RuleFor(x => x.Time)
      .TimeValid();
    this.RuleFor(x => x.Limit)
      .Limit();
    this.RuleFor(x => x.Skip)
      .Skip();
  }
}
