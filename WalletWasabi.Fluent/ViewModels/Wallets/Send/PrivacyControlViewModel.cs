using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
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
	[AutoNotify] private bool _mustHave;

	public LabelViewModel(PrivacyControlViewModel owner, string label)
	{
		Value = label;

		this.WhenAnyValue(x => x.IsPointerOver)
			.Skip(1)
			.Subscribe(isPointerOver => owner.OnPointerOver(this, isPointerOver));

		ClickedCommand = ReactiveCommand.Create(() => owner.SwapLabel(this),
			this.WhenAnyValue(x => x.MustHave, x => x.IsBlackListed).Select(x => x.Item2 || !x.Item1 && !x.Item2));
	}

	public string Value { get; }

	public ICommand ClickedCommand { get; }
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

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: false);
		EnableBack = true;

		NextCommand = ReactiveCommand.Create(() => Complete(GetUsedPockets()));
	}

	public Pocket[] AllPocket { get; set; } = Array.Empty<Pocket>();

	public IEnumerable<LabelViewModel> AllLabelViewModel { get; set; } = Array.Empty<LabelViewModel>();

	public IEnumerable<LabelViewModel> LabelsWhiteList => AllLabelViewModel.Where(x => !x.IsBlackListed);

	public IEnumerable<LabelViewModel> LabelsBlackList => AllLabelViewModel.Where(x => x.IsBlackListed);

	private IEnumerable<Pocket> GetUsedPockets() => AllPocket.Where(x => LabelsWhiteList.Any(y => x.Labels.Contains(y.Value)));

	public LabelViewModel[] GetAssociatedLabels(LabelViewModel labelViewModel)
	{
		var associatedPockets = AllPocket.Where(x => x.Labels.Contains(labelViewModel.Value));

		if (labelViewModel.IsBlackListed)
		{
			var allAssociatedLabels = SmartLabel.Merge(associatedPockets.Select(x => x.Labels));
			var affectedLabelViewModels = AllLabelViewModel.Where(x => x.IsBlackListed == labelViewModel.IsBlackListed && allAssociatedLabels.Contains(x.Value));
			return affectedLabelViewModels.ToArray();
		}
		else
		{
			var notAssociatedPockets = AllPocket.Except(associatedPockets);
			var allNotAssociatedLabels = SmartLabel.Merge(notAssociatedPockets.Select(x => x.Labels));
			var affectedLabelViewModels = AllLabelViewModel.Where(x => x.IsBlackListed == labelViewModel.IsBlackListed && !allNotAssociatedLabels.Contains(x.Value));
			return affectedLabelViewModels.ToArray();
		}
	}

	public void OnPointerOver(LabelViewModel labelViewModel, bool isPointerOver)
	{
		if (!isPointerOver)
		{
			foreach (LabelViewModel lvm in AllLabelViewModel)
			{
				lvm.IsHighlighted = false;
			}

			return;
		}

		var affectedLabelViewModels = GetAssociatedLabels(labelViewModel);

		foreach (var lvm in affectedLabelViewModels)
		{
			lvm.IsHighlighted = isPointerOver;
		}
	}

	internal void SwapLabel(LabelViewModel labelViewModel)
	{
		var affectedLabelViewModels = GetAssociatedLabels(labelViewModel);

		foreach (var lvm in affectedLabelViewModels)
		{
			lvm.IsBlackListed = !lvm.IsBlackListed;
		}

		OnSelectionChanged();
	}

	private void Complete(IEnumerable<Pocket> pockets)
	{
		var coins = pockets.SelectMany(x => x.Coins);

		Close(DialogResultKind.Normal, coins);
	}

	private void OnSelectionChanged()
	{
		var sumOfWhiteList = AllPocket.Where(x => LabelsWhiteList.Any(y => x.Labels.Contains(y.Value))).Sum(x => x.Amount);

		foreach (var labelViewModel in LabelsWhiteList)
		{
			var sumOfLabelsPockets = AllPocket.Where(x => x.Labels.Contains(labelViewModel.Value)).Sum(x => x.Amount);

			labelViewModel.MustHave = sumOfWhiteList - sumOfLabelsPockets < _transactionInfo.Amount;
		}

		this.RaisePropertyChanged(nameof(LabelsWhiteList));
		this.RaisePropertyChanged(nameof(LabelsBlackList));
	}

	private void InitializeLabels()
	{
		AllPocket = _wallet.Coins.GetPockets(_wallet.ServiceConfiguration.MinAnonScoreTarget).Select(x => new Pocket(x)).ToArray();

		var allLabels = SmartLabel.Merge(AllPocket.Select(x => x.Labels));
		AllLabelViewModel = allLabels.Select(x => new LabelViewModel(this, x)).ToArray();

		OnSelectionChanged();
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
