namespace App.StartUp.Registry;

public class AuthPolicies
{
  public const string OnlyAdmin = "OnlyAdmin";
  public const string OnlyPoller = "OnlyPoller";
  public const string OnlyLoginer = "OnlyLoginer";
  public const string OnlyReserver = "OnlyReserver";
  public const string OnlyBuyer = "OnlyBuyer";
  public const string OnlyCountSyncer = "OnlyCountSyncer";
}

public class AuthRoles
{
  public const string Admin = "admin";
  public const string Poller = "copper";
  public const string Loginer = "lithium";
  public const string Reserver = "sulfur";
  public const string Buyer = "radon";
  public const string CountSyncer = "argon";

  public const string Field = "roles";
}
