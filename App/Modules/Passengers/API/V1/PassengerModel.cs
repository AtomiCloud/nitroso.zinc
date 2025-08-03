using App.Modules.Users.API.V1;

namespace App.Modules.Passengers.API.V1;

public record SearchPassengerQuery(string? UserId, string? Name, int? Limit, int? Skip);

// REQ
public record CreatePassengerReq(
  string FullName,
  string Gender,
  string PassportExpiry,
  string PassportNumber
);

public record UpdatePassengerReq(
  string FullName,
  string Gender,
  string PassportExpiry,
  string PassportNumber
);

// RESP
public record PassengerPrincipalRes(
  Guid Id,
  string FullName,
  string Gender,
  string PassportExpiry,
  string PassportNumber
);

public record PassengerRes(PassengerPrincipalRes Principal, UserPrincipalRes User);
