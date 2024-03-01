namespace App.Modules.Payments.Airwallex;

public record AuthenticatorToken(string Secret, DateTime Expiry);
