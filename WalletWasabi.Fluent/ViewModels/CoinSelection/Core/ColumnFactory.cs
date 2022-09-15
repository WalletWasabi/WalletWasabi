using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using DynamicData;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using ISelectable = WalletWasabi.Fluent.Controls.ISelectable;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection.Core;

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
				CompareDescending = SortDescending<WalletCoinViewModel, Money>(model => model.Amount)
			});
	}

	public static TemplateColumn<TreeNode> AnonymityScore()
	{
		return new TemplateColumn<TreeNode>(
			new AnonymityScoreHeaderViewModel(),
			new ConstantTemplate<TreeNode>(
				group => group.Value switch
				{
					WalletCoinViewModel coin => new AnonymityScoreCellViewModel(coin),
					CoinGroupViewModel { Items.Count: 1 } cg => new AnonymityScoreCellViewModel((WalletCoinViewModel)cg.Items.First()),
					_ => ""
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, int>(model => model.AnonymitySet),
				CompareDescending = SortDescending<WalletCoinViewModel, int>(model => model.AnonymitySet)
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
						return new LabelsCellViewModel(vm.SmartLabel);
					}

					return new LabelsCellViewModel(new SmartLabel());
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, SmartLabel>(c => c.SmartLabel),
				CompareDescending = SortDescending<WalletCoinViewModel, SmartLabel>(c => c.SmartLabel)
			});
	}

	public static TemplateColumn<TreeNode> LabelsColumnForGroups()
	{
		return new TemplateColumn<TreeNode>(
			"Labels (Cluster)",
			new ConstantTemplate<TreeNode>(
				group => group.Value switch
				{
					CoinGroupViewModel { PrivacyLevel: null } vm => new LabelsCellViewModel(vm.Labels),
					CoinGroupViewModel vm => GetLabelFromPrivacyLevel(vm.PrivacyLevel.Value),
					_ => new LabelsCellViewModel(new SmartLabel())
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<CoinGroupViewModel, PrivacyLevelKey>(c => c.Key),
				CompareDescending = SortDescending<CoinGroupViewModel, PrivacyLevelKey>(c => c.Key)
			});
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Using DisposeWith")]
	public static TemplateColumn<TreeNode> SelectionColumn(
		IObservable<IChangeSet<ISelectable, OutPoint>> items,
		IEnumerable<CommandViewModel> commands,
		CompositeDisposable disposables)
	{
		var selectionHeaderViewModel = new SelectionHeaderViewModel(
			items.RemoveKey(),
			i => i > 99 ? "99+" : i.ToString(),
			commands);
		selectionHeaderViewModel.DisposeWith(disposables);

		return new TemplateColumn<TreeNode>(
			selectionHeaderViewModel,
			new ConstantTemplate<TreeNode>(
				n => n.Value switch
					{
						CoinGroupViewModel cg => new SelectableCollectionCellViewModel(cg.Items),
						WalletCoinViewModel coin => new CoinSelectionCellViewModel(coin),
						_ => throw new NotSupportedException()
					}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, bool>(model => model.IsSelected),
				CompareDescending = SortDescending<WalletCoinViewModel, bool>(model => model.IsSelected)
			});
	}

	public static TemplateColumn<TreeNode> IndicatorsColumn()
	{
		return new TemplateColumn<TreeNode>(
			"",
			new ConstantTemplate<TreeNode>(
				n => n.Value switch
				{
					WalletCoinViewModel coin => new IndicatorsCellViewModel(coin),
					CoinGroupViewModel { Items.Count: 1 } cg => new IndicatorsCellViewModel((WalletCoinViewModel)cg.Items.First()),
					_ => ""
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<WalletCoinViewModel, int>(GetIndicatorPriority),
				CompareDescending = SortDescending<WalletCoinViewModel, int>(GetIndicatorPriority)
			});
	}

	public static HierarchicalExpanderColumn<TreeNode> ChildrenColumn(TemplateColumn<TreeNode> textColumn)
	{
		return new HierarchicalExpanderColumn<TreeNode>(
			textColumn,
			group => group.Children,
			node => node.Children.Count() > 1);
	}

	private static string GetLabelFromPrivacyLevel(PrivacyLevel privacyLevel)
	{
		return privacyLevel switch
		{
			PrivacyLevel.None => "(Invalid privacy level)",
			PrivacyLevel.SemiPrivate => "Semi-private coins",
			PrivacyLevel.Private => "Private coins",
			PrivacyLevel.NonPrivate => "",
			_ => throw new ArgumentOutOfRangeException(nameof(privacyLevel), privacyLevel, null)
		};
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
