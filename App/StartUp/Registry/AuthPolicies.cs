namespace App.StartUp.Registry;

public class AuthPolicies
{
  public const string OnlyAdmin = "OnlyAdmin";

  public const string AdminOrTin = "AdminOrTin";
}

public class AuthRoles
{
  public const string Field = "roles";
  public const string Admin = "admin";
  public const string Tin = "tin";

  // unlocks full analysis/P&L history; admins without it are clamped to
  // RangeClamp.NonOwnerFloor onward (matched case-insensitively)
  public const string Owner = "owner";
}
