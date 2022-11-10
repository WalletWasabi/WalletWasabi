using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class LabelSelectionViewModel : ViewModelBase
{
	private readonly KeyManager _keyManager;
	private readonly string _password;
	private readonly TransactionInfo _info;
	private readonly bool _isSilent;
	private readonly Money _targetAmount;
	private readonly FeeRate _feeRate;
	private readonly List<Pocket> _hiddenIncludedPockets = new();

	[AutoNotify] private bool _enoughSelected;

	private Pocket _privatePocket = Pocket.Empty;
	private Pocket _semiPrivatePocket = Pocket.Empty;
	private Pocket[] _allPockets = Array.Empty<Pocket>();

	public LabelSelectionViewModel(KeyManager keyManager, string password, TransactionInfo info, bool isSilent)
	{
		_keyManager = keyManager;
		_password = password;
		_info = info;
		_isSilent = isSilent;
		_targetAmount = _info.Amount;
		_feeRate = _info.FeeRate;
	}

	public Pocket[] NonPrivatePockets { get; set; } = Array.Empty<Pocket>();

	public IEnumerable<LabelViewModel> AllLabelsViewModel { get; set; } = Array.Empty<LabelViewModel>();

	public IEnumerable<LabelViewModel> LabelsWhiteList => AllLabelsViewModel.Where(x => !x.IsBlackListed);

	public IEnumerable<LabelViewModel> LabelsBlackList => AllLabelsViewModel.Where(x => x.IsBlackListed);

	private bool IsPocketEnough(params Pocket[] pockets)
	{
		var coins = Pocket.Merge(pockets).Coins;
		var allCoins = Pocket.Merge(_allPockets).Coins;

		return TransactionHelpers.TryBuildTransactionWithoutPrevTx(
			keyManager: _keyManager,
			transactionInfo: _info,
			allCoins: new CoinsView(allCoins),
			allowedCoins: coins,
			password: _password,
			minimumAmount: out _);
	}

	public Pocket[] AutoSelectPockets()
	{
		var privateAndSemiPrivatePockets = new[] { _privatePocket, _semiPrivatePocket };

		var knownPockets = NonPrivatePockets.Where(x => x.Labels != CoinPocketHelper.UnlabelledFundsText).ToArray();
		var unknownPockets = NonPrivatePockets.Except(knownPockets).ToArray();

		var privateAndSemiPrivateAndUnknownPockets = privateAndSemiPrivatePockets.Union(unknownPockets).ToArray();
		var privateAndSemiPrivateAndKnownPockets = privateAndSemiPrivatePockets.Union(knownPockets).ToArray();

		var knownByRecipientPockets = knownPockets.Where(pocket => pocket.Labels.Any(label => _info.Recipient.Contains(label, StringComparer.OrdinalIgnoreCase))).ToArray();
		var onlyKnownByRecipientPockets = knownByRecipientPockets.Where(pocket => pocket.Labels.Equals(_info.Recipient, StringComparer.OrdinalIgnoreCase)).ToArray();

		if (IsPocketEnough(onlyKnownByRecipientPockets))
		{
			return onlyKnownByRecipientPockets;
		}

		if (IsPocketEnough(_privatePocket))
		{
			return new[] { _privatePocket };
		}

		if (IsPocketEnough(privateAndSemiPrivatePockets))
		{
			return privateAndSemiPrivatePockets;
		}

		if (TryGetBestKnownByRecipientPocketsWithPrivateAndSemiPrivatePockets(knownByRecipientPockets, privateAndSemiPrivatePockets, _targetAmount, _feeRate, _info.Recipient, out var pockets))
		{
			return pockets;
		}

		if (IsPocketEnough(privateAndSemiPrivateAndKnownPockets))
		{
			return privateAndSemiPrivateAndKnownPockets;
		}

		if (IsPocketEnough(privateAndSemiPrivateAndUnknownPockets))
		{
			return privateAndSemiPrivateAndUnknownPockets;
		}

		return _allPockets.ToArray();
	}

	private bool TryGetBestKnownByRecipientPocketsWithPrivateAndSemiPrivatePockets(Pocket[] knownByRecipientPockets, Pocket[] privateAndSemiPrivatePockets, Money targetAmount, FeeRate feeRate, SmartLabel recipient, [NotNullWhen(true)] out Pocket[]? pockets)
	{
		pockets = null;

		if (!IsPocketEnough(Pocket.Merge(knownByRecipientPockets, privateAndSemiPrivatePockets)))
		{
			return false;
		}

		var privacyRankedPockets =
			knownByRecipientPockets
				.Select(pocket =>
				{
					var containedRecipientLabelsCount = pocket.Labels.Count(label => recipient.Contains(label, StringComparer.OrdinalIgnoreCase));
					var totalPocketLabelsCount = pocket.Labels.Count();
					var totalRecipientLabelsCount = recipient.Count();
					var index = ((double)containedRecipientLabelsCount / totalPocketLabelsCount) + ((double)containedRecipientLabelsCount / totalRecipientLabelsCount);

					return (acceptabilityIndex: index, pocket);
				})
				.OrderByDescending(tup => tup.acceptabilityIndex)
				.ThenBy(tup => tup.pocket.Labels.Count())
				.ThenByDescending(tup => tup.pocket.Amount)
				.Select(tup => tup.pocket)
				.ToArray();

		var bestPockets = new List<Pocket>();
		bestPockets.AddRange(privateAndSemiPrivatePockets);

		// Iterate through the ordered by privacy pockets and add them one by one until the total amount covers the payment.
		// The first one is the best from the privacy point of view, and the last one is the worst.
		foreach (var p in privacyRankedPockets)
		{
			bestPockets.Add(p);

			if (IsPocketEnough(bestPockets.ToArray()))
			{
				break;
			}
		}

		// It can happen that there are unnecessary selected pockets, so remove the ones that are not needed.
		// Use Except to make sure the private and semi private pockets never get removed.
		// Example for an over selection:
		// Privacy ordered pockets: [A - 3 BTC] [B - 1 BTC] [C - 2 BTC] (A is the best for privacy, C is the worst)
		// Target amount is 4.5 BTC so the algorithm will select all because it happened in privacy order.
		// But B is unnecessary because A and C can cover the case, so remove it.
		foreach (var p in bestPockets.Except(privateAndSemiPrivatePockets).OrderBy(x => x.Amount).ThenByDescending(x => x.Labels.Count()).ToImmutableArray())
		{
			if (IsPocketEnough(bestPockets.Except(new[] { p }).ToArray()))
			{
				bestPockets.Remove(p);
			}
			else
			{
				break;
			}
		}

		pockets = bestPockets.ToArray();
		return true;
	}

	public Pocket[] GetUsedPockets() =>
		NonPrivatePockets.Where(x => x.Labels.All(label => LabelsWhiteList.Any(labelViewModel => labelViewModel.Value == label)))
			.Union(_hiddenIncludedPockets)
			.ToArray();

	public void Reset(Pocket[] pockets)
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

		if (!_isSilent)
		{
			OnSelectionChanged();
		}
	}

	public LabelViewModel[] GetAssociatedLabels(LabelViewModel labelViewModel)
	{
		if (labelViewModel.IsBlackListed)
		{
			var associatedPocketLabels = NonPrivatePockets.OrderBy(x => x.Labels.Count()).First(x => x.Labels.Contains(labelViewModel.Value)).Labels;
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

	public void SwapLabel(LabelViewModel labelViewModel)
	{
		var affectedLabelViewModels = GetAssociatedLabels(labelViewModel);

		foreach (var lvm in affectedLabelViewModels)
		{
			lvm.Swap();
		}

		OnSelectionChanged();
	}

	private void OnSelectionChanged()
	{
		_hiddenIncludedPockets.Clear();

		var (isPrivateNeeded, isSemiPrivateNeeded) = ArePrivateAndSemiPrivatePocketsNeeded();

		if (isPrivateNeeded)
		{
			_hiddenIncludedPockets.Add(_privatePocket);
		}

		if (isSemiPrivateNeeded)
		{
			_hiddenIncludedPockets.Add(_semiPrivatePocket);
		}

		EnoughSelected = IsPocketEnough(GetUsedPockets());

		this.RaisePropertyChanged(nameof(LabelsWhiteList));
		this.RaisePropertyChanged(nameof(LabelsBlackList));
	}

	private (bool, bool) ArePrivateAndSemiPrivatePocketsNeeded()
	{
		var isPrivateNeeded = false;
		var isSemiPrivateNeeded = false;

		var usedPockets = GetUsedPockets();
		var usedPocketsLabels = new SmartLabel(usedPockets.SelectMany(p => p.Labels));

		if (usedPocketsLabels != _info.Recipient || !IsPocketEnough(usedPockets))
		{
			isPrivateNeeded = true;

			if (!IsPocketEnough(Pocket.Merge(usedPockets, _privatePocket)))
			{
				isSemiPrivateNeeded = true;
			}
		}

		return (isPrivateNeeded, isSemiPrivateNeeded);
	}

	public void SetUsedLabel(IEnumerable<SmartCoin>? usedCoins, int privateThreshold)
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

		OnSelectionChanged();
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
