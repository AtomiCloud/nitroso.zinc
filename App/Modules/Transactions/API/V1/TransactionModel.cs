using App.Modules.Users.API.V1;
using App.Modules.Wallets.API.V1;

namespace App.Modules.Transactions.API.V1;

public record SearchTransactionQuery(
 string? Search,
 string? TransactionType,
 Guid? Id,
 Guid? WalletId,
 string? userId,

 // Only use date
 string? Before,
 string? After,

 int? Limit,
 int? Skip
);

// RESP
public record TransactionPrincipalRes(Guid Id, DateTime CreatedAt, string Name, string Description, string TransactionType, decimal Amount, string From, string To);

public record TransactionRes(TransactionPrincipalRes Principal, WalletPrincipalRes Wallet);
