using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;

public static class ColumnFactory
{
	public static TemplateColumn<TreeNode> AmountColumn()
	{
		return new TemplateColumn<TreeNode>(
			"Amount",
			new ObservableTemplate<TreeNode, string>(
				group =>
				{
					return group.Value switch
					{
						CoinGroupViewModel cg => cg.TotalAmount.Select(x => x.ToFormattedString()),
						WalletCoinViewModel coin => new BehaviorSubject<string>(coin.Amount.ToFormattedString()),
						_ => Observable.Return("")
					};
				}));
	}

	public static TemplateColumn<TreeNode> AnonymityScore()
	{
		return new TemplateColumn<TreeNode>(
			new AnonymityScoreHeaderViewModel(),
			new ObservableTemplate<TreeNode, int>(
				group =>
				{
					return group.Value switch
					{
						WalletCoinViewModel coin => new BehaviorSubject<int>(coin.AnonymitySet),
						_ => throw new NotSupportedException()
					};
				}));
	}

	public static TemplateColumn<TreeNode> LabelsColumnForCoins()
	{
		return new TemplateColumn<TreeNode>(
			"Labels",
			new ConstantTemplate<TreeNode>(
				group =>
				{
					if (group.Value is WalletCoinViewModel vm)
					{
						return new LabelsViewModel(vm.SmartLabel);
					}

					return new LabelsViewModel(new SmartLabel());
				}));
	}

	public static TemplateColumn<TreeNode> LabelsColumnForGroups()
	{
		return new TemplateColumn<TreeNode>(
			"Labels (Cluster)",
			new ConstantTemplate<TreeNode>(
				group =>
				{
					if (group.Value is CoinGroupViewModel vm)
					{
						return new LabelsViewModel(vm.Labels);
					}

					return new LabelsViewModel(new SmartLabel());
				}));
	}

	public static TemplateColumn<TreeNode> SelectionColumn(Action<IDisposable> onCellCreated)
	{
		return new TemplateColumn<TreeNode>(
			"",
			new ConstantTemplate<TreeNode>(
				n =>
				{
					if (n.Value is CoinGroupViewModel cg)
					{
						var isSelectedViewModel = new IsSelectedThreeStateViewModel(cg);
						onCellCreated(isSelectedViewModel);
						return isSelectedViewModel;
					}

					if (n.Value is WalletCoinViewModel coin)
					{
						var isSelectedViewModel = new IsSelectedViewModel(coin);
						onCellCreated(isSelectedViewModel);
						return isSelectedViewModel;
					}

					throw new NotSupportedException();
				}));
	}

	public static HierarchicalExpanderColumn<TreeNode> ChildrenColumn()
	{
		return new HierarchicalExpanderColumn<TreeNode>(
			new TextColumn<TreeNode, string>("", group => ""),
			group => group.Children,
			node => node.Children.Any());
	}
}
