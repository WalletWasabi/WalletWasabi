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

public partial class LabelViewModel : ViewModelBase
{
	[AutoNotify] private bool _isBlackListed;
	[AutoNotify] private bool _isPointerOver;
	[AutoNotify] private bool _isHighlighted;

	public LabelViewModel(PrivacyControlViewModel owner, string label)
	{
		Pockets = new List<PocketViewModel>();
		Value = label;

		this.WhenAnyValue(x => x.IsPointerOver)
			.Subscribe(isPointerOver =>
			{
				foreach (var pocket in Pockets)
				{
					foreach (var pocketLabel in pocket.Labels)
					{
						pocketLabel.IsHighlighted = isPointerOver;
					}
				}
			});

		ClickedCommand = ReactiveCommand.Create(() =>
		{
			foreach (var pocket in Pockets)
			{
				foreach (var pocketLabel in pocket.Labels)
				{
					pocketLabel.IsHighlighted = false;
				}
			}

			owner.SwapLabel(this);
		});
	}

	public List<PocketViewModel> Pockets { get; }

	public string Value { get; }

	public ICommand ClickedCommand { get; }
}

public class PocketViewModel : ViewModelBase
{
	public PocketViewModel()
	{
		Labels = new List<LabelViewModel>();
	}

	public List<LabelViewModel> Labels { get; }
}

[NavigationMetaData(Title = "Privacy Control")]
public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly bool _isSilent;

	[AutoNotify] private Pocket[] _usedPockets = Array.Empty<Pocket>();

	private bool _isUpdating;

	public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
	{
		_wallet = wallet;
		_transactionInfo = transactionInfo;
		_isSilent = isSilent;

		Labels = new ObservableCollection<string>();
		MustHaveLabels = new ObservableCollection<string>();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => Complete(UsedPockets));

		Observable
			.FromEventPattern(Labels, nameof(Labels.CollectionChanged))
			.Subscribe(_ => OnLabelsChanged());
	}

	internal void SwapLabel(LabelViewModel label)
	{
		if (label.IsBlackListed)
		{
			foreach (var pocket in label.Pockets)
			{
				WhiteListPocketLabels(pocket);
			}
		}
		else
		{
			foreach (var pocket in label.Pockets)
			{
				BlackListPocketLabels(pocket);
			}
		}
	}

	private void WhiteListPocketLabels(PocketViewModel pocket, bool initialising = false)
	{
		var labels = pocket.Labels.Where(x => x.IsBlackListed);

		foreach (var label in labels)
		{
			LabelsBlackList.Remove(label);

			label.IsBlackListed = false;

			LabelsWhiteList.Add(label);
		}
	}

	private void BlackListPocketLabels(PocketViewModel pocket)
	{
		foreach (var label in pocket.Labels.Where(x => !x.IsBlackListed))
		{
			LabelsWhiteList.Remove(label);

			label.IsBlackListed = true;

			LabelsBlackList.Add(label);
		}
	}

	public ObservableCollection<LabelViewModel> LabelsWhiteList { get; } = new();

	public ObservableCollection<LabelViewModel> LabelsBlackList { get; } = new();

	public ObservableCollection<string> Labels { get; set; }

	public ObservableCollection<string> MustHaveLabels { get; }

	private void Complete(IEnumerable<Pocket> pockets)
	{
		Close(DialogResultKind.Normal, pockets.SelectMany(x => x.Coins));
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
			var newPockets = UsedPockets.Except(pocketsToRemove);

			UpdateLabels(newPockets);
		});
	}

	private void UpdateLabels(IEnumerable<Pocket> pockets)
	{
		_isUpdating = true;

		var labelViewModels = new Dictionary<string, LabelViewModel>();

		var pocketVms = new List<PocketViewModel>();

		foreach (var pocket in pockets)
		{
			var pocketVm = new PocketViewModel();

			pocketVms.Add(pocketVm);

			foreach (var label in pocket.Labels)
			{
				if (!labelViewModels.ContainsKey(label))
				{
					labelViewModels[label] = new LabelViewModel(this, label) { IsBlackListed = true };
				}

				pocketVm.Labels.Add(labelViewModels[label]);

				labelViewModels[label].Pockets.Add(pocketVm);
			}
		}

		LabelsWhiteList.Clear();
		LabelsBlackList.Clear();

		foreach (var pocketVm in pocketVms)
		{
			WhiteListPocketLabels(pocketVm, initialising: true);
		}

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
