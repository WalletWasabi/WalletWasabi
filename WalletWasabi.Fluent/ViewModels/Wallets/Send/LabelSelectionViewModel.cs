using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class LabelSelectionViewModel : ViewModelBase
{
	private readonly KeyManager _keyManager;
	private readonly string _password;
	private readonly TransactionInfo _info;
	private readonly List<Pocket> _hiddenIncludedPockets = new();

	[AutoNotify] private bool _enoughSelected;

	private Pocket _privatePocket = Pocket.Empty;
	private Pocket _semiPrivatePocket = Pocket.Empty;
	private Pocket[] _allPockets = Array.Empty<Pocket>();

	public LabelSelectionViewModel(KeyManager keyManager, string password, TransactionInfo info)
	{
		_keyManager = keyManager;
		_password = password;
		_info = info;
	}

	public Pocket[] NonPrivatePockets { get; set; } = Array.Empty<Pocket>();

	public IEnumerable<LabelViewModel> AllLabelsViewModel { get; set; } = Array.Empty<LabelViewModel>();

	public IEnumerable<LabelViewModel> LabelsWhiteList => AllLabelsViewModel.Where(x => !x.IsBlackListed);

	public IEnumerable<LabelViewModel> LabelsBlackList => AllLabelsViewModel.Where(x => x.IsBlackListed);

	private async Task<bool> IsPocketEnoughAsync(params Pocket[] pockets)
	{
		var coins = Pocket.Merge(pockets).Coins;
		var allCoins = Pocket.Merge(_allPockets).Coins;

		return await Task.Run(() => TransactionHelpers.TryBuildTransactionWithoutPrevTx(
			keyManager: _keyManager,
			transactionInfo: _info,
			allCoins: new CoinsView(allCoins),
			allowedCoins: coins,
			password: _password,
			minimumAmount: out _));
	}

	public Pocket[] GetUsedPockets() =>
		NonPrivatePockets.Where(x => x.Labels.All(label => LabelsWhiteList.Any(labelViewModel => labelViewModel.Value == label)))
			.Union(_hiddenIncludedPockets)
			.ToArray();

	public async Task ResetAsync(Pocket[] pockets)
	{
		_allPockets = pockets;

		if (pockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText) is { } privatePocket)
		{
			_privatePocket = privatePocket;
		}

		if (pockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.SemiPrivateFundsText) is { } semiPrivatePocket)
		{
			_semiPrivatePocket = semiPrivatePocket;
		}

		NonPrivatePockets = pockets.Where(x => x != _privatePocket && x != _semiPrivatePocket).ToArray();

		var allLabels = SmartLabel.Merge(NonPrivatePockets.Select(x => x.Labels));
		AllLabelsViewModel = allLabels.Select(x => new LabelViewModel(this, x)).ToArray();

		if (AllLabelsViewModel.FirstOrDefault(x => x.Value == CoinPocketHelper.UnlabelledFundsText) is { } unlabelledViewModel)
		{
			unlabelledViewModel.IsDangerous = true;
			unlabelledViewModel.ToolTip = "There is no information about these people, only use it when necessary!";
		}

		await OnSelectionChangedAsync();
	}

	public LabelViewModel[] GetAssociatedLabels(LabelViewModel labelViewModel)
	{
		if (labelViewModel.IsBlackListed)
		{
			var associatedPocketLabels = NonPrivatePockets.OrderBy(x => x.Labels.Count).First(x => x.Labels.Contains(labelViewModel.Value)).Labels;
			return LabelsBlackList.Where(x => associatedPocketLabels.Contains(x.Value)).ToArray();
		}
		else
		{
			var associatedPockets = NonPrivatePockets.Where(x => x.Labels.Contains(labelViewModel.Value));
			var notAssociatedPockets = NonPrivatePockets.Except(associatedPockets);
			var allNotAssociatedLabels = SmartLabel.Merge(notAssociatedPockets.Select(x => x.Labels));
			return LabelsWhiteList.Where(x => !allNotAssociatedLabels.Contains(x.Value)).ToArray();
		}
	}

	public void OnFade(LabelViewModel source)
	{
		foreach (var lvm in source.IsBlackListed ? LabelsBlackList : LabelsWhiteList)
		{
			if (!lvm.IsHighlighted)
			{
				lvm.Fade(source);
			}
		}
	}

	public void OnPointerOver(LabelViewModel labelViewModel)
	{
		var affectedLabelViewModels = GetAssociatedLabels(labelViewModel);

		foreach (var lvm in affectedLabelViewModels)
		{
			lvm.Highlight(triggerSource: labelViewModel);
		}
	}

	public async Task SwapLabelAsync(LabelViewModel labelViewModel)
	{
		var affectedLabelViewModels = GetAssociatedLabels(labelViewModel);

		foreach (var lvm in affectedLabelViewModels)
		{
			lvm.Swap();
		}

		await OnSelectionChangedAsync();
	}

	private async Task OnSelectionChangedAsync()
	{
		_hiddenIncludedPockets.Clear();

		var (isPrivateNeeded, isSemiPrivateNeeded) = await ArePrivateAndSemiPrivatePocketsNeededAsync();

		if (isPrivateNeeded)
		{
			_hiddenIncludedPockets.Add(_privatePocket);
		}

		if (isSemiPrivateNeeded)
		{
			_hiddenIncludedPockets.Add(_semiPrivatePocket);
		}

		EnoughSelected = await IsPocketEnoughAsync(GetUsedPockets());

		this.RaisePropertyChanged(nameof(LabelsWhiteList));
		this.RaisePropertyChanged(nameof(LabelsBlackList));
	}

	private async Task<(bool, bool)> ArePrivateAndSemiPrivatePocketsNeededAsync()
	{
		var isPrivateNeeded = false;
		var isSemiPrivateNeeded = false;

		var usedPockets = GetUsedPockets();
		var usedPocketsLabels = new SmartLabel(usedPockets.SelectMany(p => p.Labels));

		if (usedPocketsLabels != _info.Recipient || !await IsPocketEnoughAsync(usedPockets))
		{
			isPrivateNeeded = true;

			if (!await IsPocketEnoughAsync(Pocket.Merge(usedPockets, _privatePocket)))
			{
				isSemiPrivateNeeded = true;
			}
		}

		return (isPrivateNeeded, isSemiPrivateNeeded);
	}

	public async Task SetUsedLabelAsync(IEnumerable<SmartCoin>? usedCoins, int privateThreshold)
	{
		if (usedCoins is null)
		{
			return;
		}

		usedCoins = usedCoins.ToImmutableArray();

		var usedLabels = SmartLabel.Merge(usedCoins.Select(x => x.GetLabels(privateThreshold)));
		var usedLabelViewModels = AllLabelsViewModel.Where(x => usedLabels.Contains(x.Value, StringComparer.OrdinalIgnoreCase)).ToArray();
		var notUsedLabelViewModels = AllLabelsViewModel.Except(usedLabelViewModels);

		foreach (LabelViewModel label in notUsedLabelViewModels)
		{
			label.Swap();
		}

		await OnSelectionChangedAsync();
	}

	public bool IsOtherSelectionPossible(IEnumerable<SmartCoin> usedCoins, SmartLabel recipient)
	{
		var usedPockets = _allPockets.Where(pocket => pocket.Coins.Any(usedCoins.Contains)).ToImmutableArray();
		var remainingUsablePockets = _allPockets.Except(usedPockets).ToList();

		// They are handled silently, do not take them into account as manually selectable pockets.
		remainingUsablePockets.Remove(_privatePocket);
		remainingUsablePockets.Remove(_semiPrivatePocket);

		if (usedPockets.Length == 1 && usedPockets.First() == _privatePocket)
		{
			return false;
		}

		if (usedPockets.Length == 1 && usedPockets.First() == _semiPrivatePocket)
		{
			return false;
		}

		if (usedPockets.Length == 2 && usedPockets.Contains(_privatePocket) && usedPockets.Contains(_semiPrivatePocket))
		{
			return false;
		}

		if (!remainingUsablePockets.Any())
		{
			return false;
		}

		var labels = SmartLabel.Merge(usedPockets.Select(x => x.Labels));
		if (labels.Equals(recipient, StringComparer.OrdinalIgnoreCase))
		{
			return false;
		}

		return true;
	}
}
