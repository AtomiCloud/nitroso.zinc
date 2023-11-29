using App.Utility;
using FluentValidation;

namespace App.Modules.Schedules.API.V1;

public class ScheduleRangeReqValidator : AbstractValidator<ScheduleRangeReq>
{
  public ScheduleRangeReqValidator()
  {
    this.RuleFor(x => x.From)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.To)
      .NotNull()
      .DateValid();
  }
}

public class ScheduleDateReqValidator : AbstractValidator<ScheduleDateReq>
{
  public ScheduleDateReqValidator()
  {
    this.RuleFor(x => x.Date)
      .NotNull()
      .DateValid();
  }
}

public class ScheduleBulkUpdateReqValidator : AbstractValidator<ScheduleBulkUpdateReq>
{
  public ScheduleBulkUpdateReqValidator()
  {
    this.RuleFor(x => x.Schedules)
      .NotNull();
    this.RuleForEach(x => x.Schedules)
.NotNull()
      .SetValidator(new SchedulePrincipalReqValidator());
  }
}


public class ScheduleRecordReqValidator : AbstractValidator<ScheduleRecordReq>
{
  public ScheduleRecordReqValidator()
  {
    this.RuleFor(x => x.Confirmed)
      .NotNull();
    this.RuleFor(x => x.JToWExcluded)
      .NotNull();
    this.RuleForEach(x => x.JToWExcluded)
      .NotNull()
      .TimeValid();

    this.RuleFor(x => x.WToJExcluded)
      .NotNull();
    this.RuleForEach(x => x.WToJExcluded)
      .NotNull()
      .TimeValid();
  }
}

public class SchedulePrincipalReqValidator : AbstractValidator<SchedulePrincipalReq>
{
  public SchedulePrincipalReqValidator()
  {
    this.RuleFor(x => x.Date)
      .NotNull()
      .DateValid();
    this.RuleFor(x => x.Record)
      .NotNull()
      .SetValidator(new ScheduleRecordReqValidator());
  }
}
