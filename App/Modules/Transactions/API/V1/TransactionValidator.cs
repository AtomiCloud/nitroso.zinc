using App.Utility;
using FluentValidation;

namespace App.Modules.Transactions.API.V1;

public class TransactionSearchQueryValidator : AbstractValidator<SearchTransactionQuery>
{
  public TransactionSearchQueryValidator()
  {
    this.RuleFor(x => x.After).NullableDateValid();
    this.RuleFor(x => x.Before).NullableDateValid();
    this.RuleFor(x => x.TransactionType).TransactionTypeValid();

    this.RuleFor(x => x.Limit).Limit();
    this.RuleFor(x => x.Skip).Skip();
  }
}
