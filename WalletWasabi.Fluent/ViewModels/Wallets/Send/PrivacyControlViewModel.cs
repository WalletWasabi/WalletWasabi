using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Privacy Control")]
public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly bool _isSilent;
	private readonly Stack<Pocket[]> _removedPocketStack;

	[AutoNotify] private Pocket[] _usedPockets = Array.Empty<Pocket>();

	private bool _isUpdating;

	public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
	{
		_wallet = wallet;
		_transactionInfo = transactionInfo;
		_isSilent = isSilent;
		_removedPocketStack = new Stack<Pocket[]>();

		Labels = new ObservableCollection<string>();
		MustHaveLabels = new ObservableCollection<string>();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		var undoCommandCanExecute = this.WhenAnyValue(x => x.Labels.Count).Select(_ => _removedPocketStack.Count > 0);
		UndoCommand = ReactiveCommand.Create(OnUndo, undoCommandCanExecute);

		NextCommand = ReactiveCommand.Create(() => Complete(UsedPockets));

		Observable
			.FromEventPattern(Labels, nameof(Labels.CollectionChanged))
			.Subscribe(_ => OnLabelsChanged());
	}

	public ICommand UndoCommand { get; }

	public ObservableCollection<string> Labels { get; set; }

	public ObservableCollection<string> MustHaveLabels { get; }

	private void Complete(IEnumerable<Pocket> pockets)
	{
		Close(DialogResultKind.Normal, pockets.SelectMany(x => x.Coins));
	}

	private void OnUndo()
	{
		var pocketsToUndo = _removedPocketStack.Pop();
		var newPockets = UsedPockets.Union(pocketsToUndo);

		UpdateLabels(newPockets);
	}

	private void OnLabelsChanged()
	{
		if (_isUpdating)
		{
			return;
		}

		var pocketsToRemove = UsedPockets
			.Where(x => x.Labels.Any(y => !Labels.Contains(y) && x.Labels.Any(y => !MustHaveLabels.Contains(y))))
			.ToArray();

		Dispatcher.UIThread.Post(() =>
		{
			_removedPocketStack.Push(pocketsToRemove);
			var newPockets = UsedPockets.Except(pocketsToRemove);

			UpdateLabels(newPockets);
		});
	}

	private void UpdateLabels(IEnumerable<Pocket> pockets)
	{
		_isUpdating = true;

		UsedPockets = pockets.ToArray();
		MustHaveLabels.Clear();
		Labels.Clear();

		foreach (var label in SmartLabel.Merge(UsedPockets.Select(x => x.Labels)))
		{
			var pocketsWithSameLabel = UsedPockets.Where(x => x.Labels.Contains(label));

			if (UsedPockets.Sum(x => x.Amount) - pocketsWithSameLabel.Sum(x => x.Amount) >= _transactionInfo.Amount)
			{
				Labels.Add(label);
			}
			else
			{
				MustHaveLabels.Add(label);
			}
		}

		_isUpdating = false;
	}

	private void InitializeLabels()
	{
		var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.MinAnonScoreTarget).Select(x => new Pocket(x));
		UpdateLabels(pockets);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (!isInHistory)
		{
			InitializeLabels();
		}

		Observable
			.FromEventPattern(_wallet.TransactionProcessor, nameof(Wallet.TransactionProcessor.WalletRelevantTransactionProcessed))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => InitializeLabels())
			.DisposeWith(disposables);

		if (_isSilent)
		{
			if (UsedPockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText) is { } privatePocket &&
			    privatePocket.Amount >= _transactionInfo.Amount)
			{
				Complete(UsedPockets.Where(x => x.Labels == CoinPocketHelper.PrivateFundsText));
			}
			else if (UsedPockets.Where(x => x.Labels != CoinPocketHelper.PrivateFundsText).Sum(x => x.Amount) >= _transactionInfo.Amount)
			{
				Complete(UsedPockets.Where(x => x.Labels != CoinPocketHelper.PrivateFundsText));
			}
			else
			{
				Complete(UsedPockets);
			}
		}
	}
}
