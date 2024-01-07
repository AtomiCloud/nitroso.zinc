namespace Domain.Transaction;

public record Account(string Id, string DisplayName);

public static class Accounts
{
  public static Account User = new("USER_DEPOSITORY", "User Deposit");
  public static Account Usable = new("USER_WALLET", "Usable Wallet");
  public static Account WithdrawReserve = new("WITHDRAW_RESERVE", "Withdraw Reserve");
  public static Account BookingReserve = new("USER_BOOKING_RESERVE", "Booking Reserve");
  public static Account BunnyBooker = new("BUNNY_BOOKER", "Bunny Booker");
}
