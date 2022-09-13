using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Threading;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels;

public partial class DebugTransactionViewModel : ViewModelBase
{
	private readonly SmartTransaction _transaction;
	private readonly IObservable<Unit> _updateTrigger;
	[AutoNotify] private DebugCoinViewModel? _selectedCoin;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private ObservableCollection<DebugCoinViewModel> _coins;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private FlatTreeDataGridSource<DebugCoinViewModel>? _coinsSource;

	public DebugTransactionViewModel(SmartTransaction transaction, IObservable<Unit> updateTrigger)
	{
		_transaction = transaction;
		_updateTrigger = updateTrigger;

		Update();

		Dispatcher.UIThread.InvokeAsync(() =>
		{
			CoinsSource = DebugTreeDataGridHelper.CreateCoinsSource(
				Coins,
				x => SelectedCoin = x);
		});
	}

	private void Update()
	{
		FirstSeen = _transaction.FirstSeen.LocalDateTime;

		TransactionId = _transaction.GetHash();

		Coins = new ObservableCollection<DebugCoinViewModel>();
	}

	public SmartTransaction Transaction => _transaction;

	public DateTimeOffset FirstSeen { get; private set; }

	public uint256 TransactionId { get; private set; }
}
