using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class LabelSelectionViewModel : ViewModelBase
{
	private readonly Money _targetAmount;

	[AutoNotify] private bool _enoughSelected;

	private Pocket _privatePocket = Pocket.Empty;
	private bool _includePrivatePocket;
	private Pocket[] _allPockets = Array.Empty<Pocket>();

	public LabelSelectionViewModel(Money targetAmount)
	{
		_targetAmount = targetAmount;
	}

	public Pocket[] NonPrivatePockets { get; set; } = Array.Empty<Pocket>();

	public IEnumerable<LabelViewModel> AllLabelsViewModel { get; set; } = Array.Empty<LabelViewModel>();

	public IEnumerable<LabelViewModel> LabelsWhiteList => AllLabelsViewModel.Where(x => !x.IsBlackListed);

	public IEnumerable<LabelViewModel> LabelsBlackList => AllLabelsViewModel.Where(x => x.IsBlackListed);

	public Pocket[] AutoSelectPockets(SmartLabel recipient)
	{
		var knownPockets = NonPrivatePockets.Where(x => x.Labels != CoinPocketHelper.UnlabelledFundsText).ToArray();
		var unknownPockets = NonPrivatePockets.Except(knownPockets).ToArray();
		var privateAndUnknownPockets = _allPockets.Except(knownPockets).ToArray();
		var privateAndKnownPockets = _allPockets.Except(unknownPockets).ToArray();
		var knownByRecipientPockets = knownPockets.Where(pocket => pocket.Labels.Any(recipient.Contains)).ToArray();
		var onlyKnownByRecipientPockets = knownByRecipientPockets.Where(pocket => pocket.Labels == recipient).ToArray();

		if (onlyKnownByRecipientPockets.Sum(x => x.Amount) >= _targetAmount)
		{
			return onlyKnownByRecipientPockets;
		}

		if (_privatePocket.Amount >= _targetAmount)
		{
			return new[] { _privatePocket };
		}

		if (GetBestKnownByRecipientPockets(knownByRecipientPockets, _targetAmount, recipient) is { } pockets)
		{
			return pockets;
		}

		if (knownPockets.Sum(x => x.Amount) >= _targetAmount)
		{
			return knownPockets;
		}

		if (unknownPockets.Sum(x => x.Amount) >= _targetAmount)
		{
			return unknownPockets;
		}

		if (NonPrivatePockets.Sum(x => x.Amount) >= _targetAmount)
		{
			return NonPrivatePockets;
		}

		if (privateAndKnownPockets.Sum(x => x.Amount) >= _targetAmount)
		{
			return privateAndKnownPockets;
		}

		if (privateAndUnknownPockets.Sum(x => x.Amount) >= _targetAmount)
		{
			return privateAndUnknownPockets;
		}

		return _allPockets.ToArray();
	}

	private Pocket[]? GetBestKnownByRecipientPockets(Pocket[] knownByRecipientPockets, Money targetAmount, SmartLabel recipient)
	{
		var privacyRankedPockets =
			knownByRecipientPockets
				.Select(pocket =>
				{
					var containedRecipientLabelsCount = pocket.Labels.Count(recipient.Contains);
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

		var pockets = new List<Pocket>();
		foreach (var p in privacyRankedPockets)
		{
			if (pockets.Sum(x => x.Amount) < targetAmount)
			{
				pockets.Add(p);
			}
			else
			{
				break;
			}
		}

		foreach (var p in pockets.OrderBy(x => x.Amount).ToImmutableArray())
		{
			if (pockets.Sum(x => x.Amount) - p.Amount >= targetAmount)
			{
				pockets.Remove(p);
			}
			else
			{
				break;
			}
		}

		return pockets.Sum(x => x.Amount) >= targetAmount ? pockets.ToArray() : null;
	}

	public Pocket[] GetUsedPockets()
	{
		var pocketsToReturn = NonPrivatePockets.Where(x => x.Labels.All(label => LabelsWhiteList.Any(labelViewModel => labelViewModel.Value == label))).ToList();

		if (_includePrivatePocket && _privatePocket is { } privatePocket)
		{
			pocketsToReturn.Add(privatePocket);
		}

		return pocketsToReturn.ToArray();
	}

	public void Reset(Pocket[] pockets)
	{
		_allPockets = pockets;

		if (pockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText) is { } privatePocket)
		{
			_privatePocket = privatePocket;
		}

		NonPrivatePockets = pockets.Where(x => x != _privatePocket).ToArray();

		var allLabels = SmartLabel.Merge(NonPrivatePockets.Select(x => x.Labels));
		AllLabelsViewModel = allLabels.Select(x => new LabelViewModel(this, x)).ToArray();

		if (AllLabelsViewModel.FirstOrDefault(x => x.Value == CoinPocketHelper.UnlabelledFundsText) is { } unlabelledViewModel)
		{
			unlabelledViewModel.IsDangerous = true;
			unlabelledViewModel.ToolTip = "There is no information about these people, only use it when necessary!";
		}

		OnSelectionChanged();
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
		Money sumOfWhiteList =
			NonPrivatePockets
				.Where(pocket => pocket.Labels.All(pocketLabel => LabelsWhiteList.Any(labelViewModel => pocketLabel == labelViewModel.Value)))
				.Sum(x => x.Amount);

		if (sumOfWhiteList >= _targetAmount)
		{
			EnoughSelected = true;
			_includePrivatePocket = false;
		}
		else if (!LabelsBlackList.Any() && sumOfWhiteList + _privatePocket.Amount >= _targetAmount)
		{
			EnoughSelected = true;
			_includePrivatePocket = true;
		}
		else if (!LabelsWhiteList.Any() && _privatePocket.Amount >= _targetAmount)
		{
			EnoughSelected = true;
			_includePrivatePocket = true;
		}
		else
		{
			EnoughSelected = false;
			_includePrivatePocket = false;
		}

		this.RaisePropertyChanged(nameof(LabelsWhiteList));
		this.RaisePropertyChanged(nameof(LabelsBlackList));
	}

	public void SetUsedLabel(IEnumerable<SmartCoin>? usedCoins, int privateThreshold)
	{
		if (usedCoins is null)
		{
			return;
		}

		var usedLabels = SmartLabel.Merge(usedCoins.Select(x => x.GetLabels(privateThreshold)));
		var usedLabelViewModels = AllLabelsViewModel.Where(x => usedLabels.Contains(x.Value)).ToArray();
		var notUsedLabelViewModels = AllLabelsViewModel.Except(usedLabelViewModels);

		foreach (LabelViewModel label in notUsedLabelViewModels)
		{
			label.Swap();
		}

		OnSelectionChanged();
	}
}
