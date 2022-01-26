using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
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
	[AutoNotify] private bool _mustHave;

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
		}, this.WhenAnyValue(x => x.MustHave, x => x.IsBlackListed).Select(x => x.Item2 || !x.Item1 && !x.Item2));
	}

	public List<PocketViewModel> Pockets { get; }

	public string Value { get; }

	public ICommand ClickedCommand { get; }
}

public class PocketViewModel : ViewModelBase
{
	public PocketViewModel(Pocket pocket)
	{
		Labels = new List<LabelViewModel>();

		Pocket = pocket;
	}

	public Pocket Pocket { get; }

	public List<LabelViewModel> Labels { get; }
}

[NavigationMetaData(Title = "Privacy Control")]
public partial class PrivacyControlViewModel : DialogViewModelBase<IEnumerable<SmartCoin>>
{
	private readonly Wallet _wallet;
	private readonly TransactionInfo _transactionInfo;
	private readonly bool _isSilent;

	public PrivacyControlViewModel(Wallet wallet, TransactionInfo transactionInfo, bool isSilent)
	{
		_wallet = wallet;
		_transactionInfo = transactionInfo;
		_isSilent = isSilent;

		Labels = new ObservableCollection<string>();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => Complete(GetUsedPockets()));
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

		OnSelectionChanged();
	}

	private void BlackListPocketLabels(PocketViewModel pocket)
	{
		foreach (var label in pocket.Labels.Where(x => !x.IsBlackListed))
		{
			LabelsWhiteList.Remove(label);

			label.IsBlackListed = true;

			LabelsBlackList.Add(label);
		}

		OnSelectionChanged();
	}

	public ObservableCollection<LabelViewModel> LabelsWhiteList { get; } = new();

	public ObservableCollection<LabelViewModel> LabelsBlackList { get; } = new();

	public ObservableCollection<string> Labels { get; set; }

	private IEnumerable<Pocket> GetUsedPockets() =>
		LabelsWhiteList.SelectMany(x => x.Pockets).Distinct().Select(x => x.Pocket);

	private void Complete(IEnumerable<Pocket> pockets)
	{
		var coins = pockets.SelectMany(x => x.Coins);

		Close(DialogResultKind.Normal, coins);
	}

	private void OnSelectionChanged()
	{
		var sumOfWhiteList = LabelsWhiteList.SelectMany(x => x.Pockets).Distinct().Sum(x => x.Pocket.Amount);

		foreach (var label in LabelsWhiteList)
		{
			var sumOfLabelsPockets = label.Pockets.Distinct().Sum(x => x.Pocket.Amount);

			var mustHave = sumOfWhiteList - sumOfLabelsPockets < _transactionInfo.Amount;

			foreach (var pocketLabel in label.Pockets.Distinct().SelectMany(x => x.Labels))
			{
				pocketLabel.MustHave = mustHave;
			}
		}
	}

	private void InitializeLabels(IEnumerable<Pocket> pockets)
	{
		var labelViewModels = new Dictionary<string, LabelViewModel>();

		var pocketVms = new List<PocketViewModel>();

		foreach (var pocket in pockets)
		{
			var pocketVm = new PocketViewModel(pocket);

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

		OnSelectionChanged();
	}

	private void InitializeLabels()
	{
		var pockets = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.MinAnonScoreTarget).Select(x => new Pocket(x));

		InitializeLabels(pockets);
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
			var usedPockets = GetUsedPockets();

			if (usedPockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText) is { } privatePocket &&
			    privatePocket.Amount >= _transactionInfo.Amount)
			{
				Complete(usedPockets.Where(x => x.Labels == CoinPocketHelper.PrivateFundsText));
			}
			else if (usedPockets.Where(x => x.Labels != CoinPocketHelper.PrivateFundsText).Sum(x => x.Amount) >= _transactionInfo.Amount)
			{
				Complete(usedPockets.Where(x => x.Labels != CoinPocketHelper.PrivateFundsText));
			}
			else
			{
				Complete(usedPockets);
			}
		}
	}
}
