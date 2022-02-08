using System.Collections.Generic;
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

	private Pocket? _privatePocket;
	private bool _includePrivatePocket;

	public LabelSelectionViewModel(Money targetAmount)
	{
		_targetAmount = targetAmount;
	}

	public Pocket[] AllPocket { get; set; } = Array.Empty<Pocket>();

	public IEnumerable<LabelViewModel> AllLabelViewModel { get; set; } = Array.Empty<LabelViewModel>();

	public IEnumerable<LabelViewModel> LabelsWhiteList => AllLabelViewModel.Where(x => !x.IsBlackListed);

	public IEnumerable<LabelViewModel> LabelsBlackList => AllLabelViewModel.Where(x => x.IsBlackListed);

	public Pocket[] GetAllPockets()
	{
		var pockets = AllPocket.ToList();

		if (_privatePocket is { } privatePocket)
		{
			pockets.Add(privatePocket);
		}

		return pockets.ToArray();
	}

	public Pocket[] GetUsedPockets()
	{
		var pocketsToReturn = AllPocket.Where(x => x.Labels.All(label => LabelsWhiteList.Any(labelViewModel => labelViewModel.Value == label))).ToList();

		if (_includePrivatePocket && _privatePocket is { } privatePocket)
		{
			pocketsToReturn.Add(privatePocket);
		}

		return pocketsToReturn.ToArray();
	}

	public void Reset(Pocket[] pockets)
	{
		_privatePocket = pockets.FirstOrDefault(x => x.Labels == CoinPocketHelper.PrivateFundsText);

		AllPocket = pockets.Where(x => x != _privatePocket).ToArray();

		var allLabels = SmartLabel.Merge(AllPocket.Select(x => x.Labels));
		AllLabelViewModel = allLabels.Select(x => new LabelViewModel(this, x)).ToArray();

		OnSelectionChanged();
	}

	public LabelViewModel[] GetAssociatedLabels(LabelViewModel labelViewModel)
	{
		if (labelViewModel.IsBlackListed)
		{
			var associatedPocketLabels = AllPocket.OrderBy(x => x.Labels.Count()).First(x => x.Labels.Contains(labelViewModel.Value)).Labels;
			return LabelsBlackList.Where(x => associatedPocketLabels.Contains(x.Value)).ToArray();
		}
		else
		{
			var associatedPockets = AllPocket.Where(x => x.Labels.Contains(labelViewModel.Value));
			var notAssociatedPockets = AllPocket.Except(associatedPockets);
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
			AllPocket
				.Where(pocket => pocket.Labels.All(pocketLabel => LabelsWhiteList.Any(labelViewModel => pocketLabel == labelViewModel.Value)))
				.Sum(x => x.Amount);

		if (sumOfWhiteList >= _targetAmount)
		{
			EnoughSelected = true;
			_includePrivatePocket = false;
		}
		else if (!LabelsBlackList.Any() && _privatePocket is { } && sumOfWhiteList + _privatePocket.Amount >= _targetAmount)
		{
			EnoughSelected = true;
			_includePrivatePocket = true;
		}
		else if (!LabelsWhiteList.Any() && _privatePocket is { } && _privatePocket.Amount >= _targetAmount)
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

		var usedPockets = AllPocket.Where(pocket => pocket.Coins.Any(usedCoins.Contains)).ToArray();
		var notUsedPockets = AllPocket.Except(usedPockets);
		var notUsedPocketsLabels = SmartLabel.Merge(notUsedPockets.Select(x => x.Labels));
		var notUsedLabelViewModels = AllLabelViewModel.Where(x => notUsedPocketsLabels.Contains(x.Value)).ToArray();

		foreach (LabelViewModel label in notUsedLabelViewModels)
		{
			label.Swap();
		}

		OnSelectionChanged();
	}
}
