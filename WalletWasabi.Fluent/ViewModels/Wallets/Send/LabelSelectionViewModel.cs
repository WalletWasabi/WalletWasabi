using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public class LabelSelectionViewModel : ViewModelBase
{
	private readonly Money _targetAmount;

	public LabelSelectionViewModel(Money targetAmount)
	{
		_targetAmount = targetAmount;
	}

	public Pocket[] AllPocket { get; set; } = Array.Empty<Pocket>();

	public IEnumerable<LabelViewModel> AllLabelViewModel { get; set; } = Array.Empty<LabelViewModel>();

	public IEnumerable<LabelViewModel> LabelsWhiteList => AllLabelViewModel.Where(x => !x.IsBlackListed);

	public IEnumerable<LabelViewModel> LabelsBlackList => AllLabelViewModel.Where(x => x.IsBlackListed);

	public IEnumerable<Pocket> GetUsedPockets() => AllPocket.Where(x => LabelsWhiteList.Any(y => x.Labels.Contains(y.Value)));

	public void Reset(Pocket[] pockets)
	{
		AllPocket = pockets;

		var allLabels = SmartLabel.Merge(AllPocket.Select(x => x.Labels));
		AllLabelViewModel = allLabels.Select(x => new LabelViewModel(this, x)).ToArray();

		OnSelectionChanged();
	}

	public LabelViewModel[] GetAssociatedLabels(LabelViewModel labelViewModel)
	{
		if (labelViewModel.IsBlackListed)
		{
			var associatedPocket = AllPocket.Where(x => x.Labels.Contains(labelViewModel.Value)).OrderBy(x => x.Labels.Count()).First();
			var associatedPocketLabels = associatedPocket.Labels;
			var affectedLabelViewModels = AllLabelViewModel.Where(x => x.IsBlackListed == labelViewModel.IsBlackListed && associatedPocketLabels.Contains(x.Value));
			return affectedLabelViewModels.ToArray();
		}
		else
		{
			var associatedPockets = AllPocket.Where(x => x.Labels.Contains(labelViewModel.Value));
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

	public void SwapLabel(LabelViewModel labelViewModel)
	{
		var affectedLabelViewModels = GetAssociatedLabels(labelViewModel);

		foreach (var lvm in affectedLabelViewModels)
		{
			lvm.IsBlackListed = !lvm.IsBlackListed;
		}

		OnSelectionChanged();
	}

	private void OnSelectionChanged()
	{
		var sumOfWhiteList = AllPocket.Where(x => LabelsWhiteList.Any(y => x.Labels.Contains(y.Value))).Sum(x => x.Amount);

		foreach (var labelViewModel in LabelsWhiteList)
		{
			var sumOfLabelsPockets = AllPocket.Where(x => x.Labels.Contains(labelViewModel.Value)).Sum(x => x.Amount);

			labelViewModel.MustHave = sumOfWhiteList - sumOfLabelsPockets < _targetAmount;
		}

		this.RaisePropertyChanged(nameof(LabelsWhiteList));
		this.RaisePropertyChanged(nameof(LabelsBlackList));
	}
}
