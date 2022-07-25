using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace WalletWasabi.Fluent.Controls.DestinationEntry.ViewModels;

public class AmountViewModel : ReactiveValidationObject
{
	private decimal _amount;

	public AmountViewModel(Func<decimal, bool> isAmountValid)
	{
		var validAmount = this.WhenAnyValue(x => x.Amount).Select(x => x > 0);

		this.ValidationRule(
			viewModel => viewModel.Amount, validAmount.Skip(1),
			"Amount should be greater than 0");

		this.ValidationRule(
			x => x.Amount,
			isAmountValid,
			"Insufficient funds to cover the amount requested");
	}
	
	public decimal Amount
	{
		get => _amount;
		set => this.RaiseAndSetIfChanged(ref _amount, value);
	}
}
