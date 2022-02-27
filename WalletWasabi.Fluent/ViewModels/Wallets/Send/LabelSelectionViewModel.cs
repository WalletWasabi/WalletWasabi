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

	public Pocket[] AutoSelectPockets()
	{
		var knownPockets = NonPrivatePockets.Where(x => x.Labels != CoinPocketHelper.UnlabelledFundsText).ToArray();
		var unknownPockets = NonPrivatePockets.Except(knownPockets).ToArray();
		var privateAndUnknownPockets = _allPockets.Except(knownPockets).ToArray();
		var privateAndKnownPockets = _allPockets.Except(unknownPockets).ToArray();

		if (_privatePocket.Amount >= _targetAmount)
		{
			return new[] { _privatePocket };
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

	public void SetUsedLabel(IEnumerable<SmartCoin>? usedCoins)
	{
		if (usedCoins is null)
		{
			return;
		}

		var usedPockets = NonPrivatePockets.Where(pocket => pocket.Coins.Any(usedCoins.Contains)).ToArray();
		var notUsedPockets = NonPrivatePockets.Except(usedPockets);
		var notUsedPocketsLabels = SmartLabel.Merge(notUsedPockets.Select(x => x.Labels));
		var notUsedLabelViewModels = AllLabelsViewModel.Where(x => notUsedPocketsLabels.Contains(x.Value)).ToArray();

		foreach (LabelViewModel label in notUsedLabelViewModels)
		{
			label.Swap();
		}

		OnSelectionChanged();
	}

	public bool IsOtherSelectionPossible(IEnumerable<SmartCoin> usedCoins)
	{
		var usedPockets = _allPockets.Where(pocket => pocket.Coins.Any(usedCoins.Contains)).ToImmutableArray();
		var remainingUsablePockets = _allPockets.Except(usedPockets).ToList();

		if (!usedPockets.Contains(_privatePocket)) // Private pocket hasn't been used. Don't deal with it then.
		{
			remainingUsablePockets.Remove(_privatePocket);
		}

		if (usedPockets.Length == 1 && usedPockets.First() == _privatePocket)
		{
			return false;
		}

		if (!remainingUsablePockets.Any())
		{
			return false;
		}

		return true;
	}
}
