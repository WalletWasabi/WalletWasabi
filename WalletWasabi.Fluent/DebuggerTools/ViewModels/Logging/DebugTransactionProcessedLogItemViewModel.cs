using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionProcessing;

namespace WalletWasabi.Fluent.DebuggerTools.ViewModels.Logging;

public partial class DebugTransactionProcessedLogItemViewModel : DebugLogItemViewModel
{
	private readonly ProcessedResult _processedResult;

	public DebugTransactionProcessedLogItemViewModel(ProcessedResult processedResult)
	{
		_processedResult = processedResult;

		Transaction = new DebugTransactionViewModel(_processedResult.Transaction, Observable.Empty<Unit>());

		IsNews = _processedResult.IsNews;

		IsOwnCoinJoin = _processedResult.IsOwnCoinJoin;

		ReceivedDusts = _processedResult.ReceivedDusts.Select(x => x.Value).ToList();

		ReceivedCoins = _processedResult.ReceivedCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		NewlyReceivedCoins = _processedResult.NewlyReceivedCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		NewlyConfirmedReceivedCoins = _processedResult.NewlyConfirmedReceivedCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		SpentCoins = _processedResult.SpentCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		NewlySpentCoins = _processedResult.NewlySpentCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		NewlyConfirmedSpentCoins = _processedResult.NewlyConfirmedSpentCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		SuccessfullyDoubleSpentCoins = _processedResult.SuccessfullyDoubleSpentCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		ReplacedCoins = _processedResult.ReplacedCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();

		RestoredCoins = _processedResult.RestoredCoins.Select(x => new DebugCoinViewModel(x, Observable.Empty<Unit>())).ToList();
	}

	public ProcessedResult ProcessedResult => _processedResult;

	public DebugTransactionViewModel Transaction { get; private set; }

	public bool IsNews { get; private set; }

	public bool IsOwnCoinJoin { get; private set; }

	public List<Money> ReceivedDusts { get; private set; }

	public List<DebugCoinViewModel> ReceivedCoins { get; private set; }

	public List<DebugCoinViewModel> NewlyReceivedCoins { get; private set; }

	public List<DebugCoinViewModel> NewlyConfirmedReceivedCoins { get; private set; }

	public List<DebugCoinViewModel> SpentCoins { get; private set; }

	public List<DebugCoinViewModel> NewlySpentCoins { get; private set; }

	public List<DebugCoinViewModel> NewlyConfirmedSpentCoins { get; private set; }

	public List<DebugCoinViewModel> SuccessfullyDoubleSpentCoins { get; private set; }

	public List<DebugCoinViewModel> ReplacedCoins { get; private set; }

	public List<DebugCoinViewModel> RestoredCoins { get; private set; }
}
