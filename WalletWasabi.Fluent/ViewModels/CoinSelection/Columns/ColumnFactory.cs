using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Model;
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
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, Money>(model => model.Amount),
				CompareDescending= SortDescending<WalletCoinViewModel, Money>(model => model.Amount),
			});
	}

	public static TemplateColumn<TreeNode> AnonymityScore()
	{
		return new TemplateColumn<TreeNode>(
			new AnonymityScoreHeaderViewModel(),
			new ConstantTemplate<TreeNode>(
				group =>
				{
					return group.Value switch
					{
						WalletCoinViewModel coin => new AnonymityScoreCellViewModel(coin),
						_ => "",
					};
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, int>(model => model.AnonymitySet),
				CompareDescending= SortDescending<WalletCoinViewModel, int>(model => model.AnonymitySet),
			});
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
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, SmartLabel>(model => model.SmartLabel),
				CompareDescending= SortDescending<WalletCoinViewModel, SmartLabel>(model => model.SmartLabel),
			});
	}

	public static TemplateColumn<TreeNode> LabelsColumnForGroups()
	{
		return new TemplateColumn<TreeNode>(
			"Labels (Cluster)",
			new ConstantTemplate<TreeNode>(
				group =>
				{
					if (group.Value is Model.CoinGroupViewModel vm)
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
					if (n.Value is Model.CoinGroupViewModel cg)
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
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, bool>(model => model.IsSelected),
				CompareDescending= SortDescending<WalletCoinViewModel, bool>(model => model.IsSelected),
			});
	}

	public static TemplateColumn<TreeNode> IndicatorsColumn()
	{
		return new TemplateColumn<TreeNode>(
			"",
			new ConstantTemplate<TreeNode>(
				n =>
				{
					if (n.Value is WalletCoinViewModel coin)
					{
						return new IndicatorsCellViewModel(coin);
					}

					return "";
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, int>(GetIndicatorPriority),
				CompareDescending= SortDescending<WalletCoinViewModel, int>(GetIndicatorPriority),
			});
	}

	public static HierarchicalExpanderColumn<TreeNode> ChildrenColumn()
	{
		return new HierarchicalExpanderColumn<TreeNode>(
			new TextColumn<TreeNode, string>("", group => ""),
			group => group.Children,
			node => node.Children.Any());
	}

	private static Comparison<TreeNode?> SortAscending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		var comparison = new Comparison<TreeNode?>(
			(node, treeNode) =>
			{
				if (node?.Value is TSource x && treeNode?.Value is TSource y)
				{
					return Comparer<TProperty>.Default.Compare(selector(x), selector(y));
				}

				return 0;
			});

		return comparison;
	}

	private static Comparison<TreeNode?> SortDescending<TSource, TProperty>(Func<TSource, TProperty> selector)
	{
		var comparison = new Comparison<TreeNode?>(
			(node, treeNode) =>
			{
				if (node?.Value is TSource x && treeNode?.Value is TSource y)
				{
					return Comparer<TProperty>.Default.Compare(selector(y), selector(x));
				}

				return 0;
			});

		return comparison;
	}

	private static int GetIndicatorPriority(WalletCoinViewModel x)
	{
		if (x.CoinJoinInProgress)
		{
			return 1;
		}

		if (x.IsBanned)
		{
			return 2;
		}

		if (!x.Confirmed)
		{
			return 3;
		}

		return 0;
	}
}
