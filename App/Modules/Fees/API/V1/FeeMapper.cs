using Domain;

namespace App.Modules.Fees.API.V1;

public static class FeeApiMapper
{
  public static FeeSpecRes ToRes(this FeeSpec spec) => new(spec.Percentage, spec.FlatAmount);

  public static FeeEventRes ToRes(this FeeChange change) =>
    new(
      change.Id,
      change.Type.ToString(),
      change.Percentage,
      change.FlatAmount,
      change.EffectiveAt
    );
}
