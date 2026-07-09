namespace Domain.Transaction;

public record Account(string Id, string DisplayName);

public static class Accounts
{
  public static Account User = new("USER_DEPOSITORY", "User Deposit");
  public static Account Usable = new("USER_WALLET", "Usable Wallet");
  public static Account WithdrawReserve = new("WITHDRAW_RESERVE", "Withdraw Reserve");
  public static Account BookingReserve = new("USER_BOOKING_RESERVE", "Booking Reserve");
  public static Account BunnyBooker = new("BUNNY_BOOKER", "Bunny Booker");
  public static Account WithdrawalFee = new(
    "BUNNY_BOOKER_WITHDRAWAL_FEE",
    "BunnyBooker Withdrawal Fee"
  );
  public static Account DepositFee = new("BUNNY_BOOKER_DEPOSIT_FEE", "BunnyBooker Deposit Fee");
  public static Account PriorityFee = new(
    "BUNNY_BOOKER_PRIORITY_FEE",
    "BunnyBooker Priority Fee"
  );
}
