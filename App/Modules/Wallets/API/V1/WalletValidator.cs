using App.Utility;
using FluentValidation;

namespace App.Modules.Wallets.API.V1;


public class WalletSearchQueryValidator : AbstractValidator<SearchWalletQuery>
{
  public WalletSearchQueryValidator()
  {
    this.RuleFor(x => x.Limit)
      .Limit();
    this.RuleFor(x => x.Skip)
      .Skip();
  }
}
