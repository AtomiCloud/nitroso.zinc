namespace App.StartUp.Registry;

public class AuthPolicies
{
  public const string OnlyAdmin = "OnlyAdmin";
  public const string AdminOrPoller = "AdminOrPoller";
  public const string AdminOrLoginer = "AdminOrLoginer";
  public const string AdminOrReserver = "AdminOrReserver";
  public const string AdminOrBuyer = "AdminOrBuyer";
  public const string AdminOrCountSyncer = "AdminOrCountSyncer";
  public const string AdminOrScheduleSyncer = "AdminOrScheduleSyncer";
}

public class AuthRoles
{
  public const string Admin = "admin";
  public const string Poller = "copper";
  public const string Loginer = "lithium";
  public const string Reserver = "sulfur";
  public const string Buyer = "radon";
  public const string CountSyncer = "tin";
  public const string ScheduleSyncer = "boron";

  public const string Field = "roles";
}
