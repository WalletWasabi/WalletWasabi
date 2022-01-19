using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

[NavigationMetaData(Title = "Privacy Control")]
public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly bool _isSilent;

	[AutoNotify] private List<PocketViewModel> _usedPockets;

	// private List<PocketViewModel> _usedPockets;
	private bool _isUpdating;

	public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
	{
		_wallet = wallet;
		_transactionInfo = transactionInfo;
		_isSilent = isSilent;

		_usedPockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.MinAnonScoreTarget).Select(x => new PocketViewModel(x)).ToList();
		Labels = new ObservableCollection<string>();
		MustHaveLabels = new ObservableCollection<string>();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(Complete);

		EnableAutoBusyOn(NextCommand);

		Observable
			.FromEventPattern(Labels, nameof(Labels.CollectionChanged))
			.Subscribe(_ =>
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
					_isUpdating = true;

					UsedPockets = UsedPockets.Except(pocketsToRemove).ToList();
					Labels.Clear();
					UpdateLabels();

					_isUpdating = false;
				});
			});
	}

	public ObservableCollection<string> Labels { get; set; }

	public ObservableCollection<string> MustHaveLabels { get; }

	private void Complete()
	{
		Close(DialogResultKind.Normal, UsedPockets.SelectMany(x => x.Coins).ToArray());
	}

	private void UpdateLabels()
	{
		foreach (var label in SmartLabel.Merge(UsedPockets.Select(x => x.Labels)))
		{
			var coinsWithSameLabel = UsedPockets.Where(x => x.Labels.Contains(label));

			if (UsedPockets.Sum(x => x.Coins.TotalAmount()) - coinsWithSameLabel.Sum(x => x.Coins.TotalAmount()) >= _transactionInfo.Amount)
			{
				Labels.Add(label);
			}
			else if (!MustHaveLabels.Contains(label))
			{
				MustHaveLabels.Add(label);
			}
		}
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		if (!isInHistory)
		{
			_isUpdating = true;
			UpdateLabels();
			_isUpdating = false;
		}

		if (UsedPockets.Count == 1)
		{
			Complete();
		}
		else if (_isSilent &&
		         UsedPockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText) is { } privatePocket &&
		         privatePocket.Coins.TotalAmount() >= _transactionInfo.Amount)
		{
			UsedPockets = UsedPockets.Where(x => x.Labels == CoinPocketHelper.PrivateFundsText).ToList();
			Complete();
		}
	}
}
