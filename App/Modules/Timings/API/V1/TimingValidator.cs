using App.Utility;
using FluentValidation;

namespace App.Modules.Timings.API.V1;

public class TrainDirectionReqValidator : AbstractValidator<TrainDirectionReq>
{
  public TrainDirectionReqValidator()
  {
    this.RuleFor(x => x.Direction)
      .NotNull()
      .TrainDirectionValid();
  }
}

public class TimingReqValidator : AbstractValidator<TimingReq>
{
  public TimingReqValidator()
  {
    this.RuleFor(x => x.Timings)
      .NotNull();
    this.RuleForEach(x => x.Timings)
      .NotNull()
      .TimeValid();
  }
}
