using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History;

[NavigationMetaData(Title = "Transaction Details")]
public partial class TransactionDetailsHostViewModel : RoutableViewModel
{
	private readonly ObservableAsPropertyHelper<TransactionDetailsViewModel> _details;
	private readonly TransactionHistoryBuilder _historyBuilder;

	public TransactionDetailsHostViewModel(TransactionSummary transactionSummary, Wallet wallet,
		IObservable<Unit> balanceChanged)
	{
		_historyBuilder = new TransactionHistoryBuilder(wallet);

		NextCommand = ReactiveCommand.Create(OnNext);
		CopyTransactionIdCommand = ReactiveCommand.CreateFromTask(OnCopyTransactionId);

		SetupCancel(false, true, true);

		_details =
			Observable
				.Return(transactionSummary)
				.Concat(balanceChanged
					.Select(_ => GetTransaction(transactionSummary.TransactionId)))
				.Select(summary => new TransactionDetailsViewModel(summary, wallet))
				.ToProperty(this, nameof(Details));
	}

	public TransactionDetailsViewModel Details => _details.Value;

	public ICommand CopyTransactionIdCommand { get; }

	private void OnNext()
	{
		Navigate().Clear();
	}

	private TransactionSummary GetTransaction(uint256 transactionId)
	{
		var txRecordList = _historyBuilder.BuildHistorySummary();
		return txRecordList.First(x => x.TransactionId == transactionId);
	}

	private async Task OnCopyTransactionId()
	{
		if (Application.Current is {Clipboard: { } clipboard})
		{
			await clipboard.SetTextAsync(Details.TransactionId);
		}
	}
}