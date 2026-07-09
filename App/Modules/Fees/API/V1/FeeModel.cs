namespace App.Modules.Fees.API.V1;

// REQ
// Queue a fee change: takes effect at EffectiveAt (immediately when null).
// Percentage + FlatAmount both zero removes the fee. Cap is an absolute SGD
// ceiling on the computed fee (null/omitted = uncapped).
public record AddFeeReq(decimal Percentage, decimal FlatAmount, DateTime? EffectiveAt, decimal? Cap);

// RESP
// the fee in effect right now, for pre-submission display
public record FeeSpecRes(decimal Percentage, decimal FlatAmount, decimal? Cap);

// a queued (or just-created) fee change event
public record FeeEventRes(
  Guid Id,
  string Type,
  decimal Percentage,
  decimal FlatAmount,
  DateTime EffectiveAt,
  decimal? Cap
);
