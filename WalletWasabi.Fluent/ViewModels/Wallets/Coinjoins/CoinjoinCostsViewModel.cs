using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;


public partial class CoinjoinCostsViewModel : ReactiveObject
{
	private readonly Func<Money?, Amount> _createAmount;

	[AutoNotify] private Amount? _totalFeeAmount;
	[AutoNotify] private Amount? _miningFeeAmount;
	[AutoNotify] private Amount? _wastedDustAmount;
	[AutoNotify] private Amount? _paymentsAmount;
	[AutoNotify] private bool _isBreakdownVisible;
	[AutoNotify] private bool _arePaymentsVisible;

	public CoinjoinCostsViewModel(Func<Money?, Amount> createAmount)
	{
		_createAmount = createAmount;
	}

	public void Update(TransactionModel transaction)
	{
		if (transaction.CoinjoinCosts is { } costs)
		{
			TotalFeeAmount = _createAmount(costs.TotalFee);
			MiningFeeAmount = _createAmount(costs.MiningFee);
			WastedDustAmount = _createAmount(costs.WastedDust);
			IsBreakdownVisible = true;

			ArePaymentsVisible = costs.PaymentsTotal != Money.Zero;
			PaymentsAmount = ArePaymentsVisible ? _createAmount(costs.PaymentsTotal) : null;
		}
		else
		{
			// allow backwards compatibility with transactions that were created before the costs were recorded
			TotalFeeAmount = _createAmount(Math.Abs(transaction.Amount));
			MiningFeeAmount = null;
			WastedDustAmount = null;
			IsBreakdownVisible = false;

			ArePaymentsVisible = false;
			PaymentsAmount = null;
		}
	}
}
