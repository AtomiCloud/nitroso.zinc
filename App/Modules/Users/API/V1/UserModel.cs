namespace App.Modules.Users.API.V1;

public record SearchUserQuery(string? Id, string? Username, int? Limit, int? Skip);

// REQ
public record CreateUserReq(string Username);

public record UpdateUserReq(string Username);

// RESP
public record UserExistRes(bool Exists);

public record UserPrincipalRes(string Id, string Username);

public record UserRes(UserPrincipalRes Principal);
