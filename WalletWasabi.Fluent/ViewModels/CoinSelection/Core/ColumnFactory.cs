using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using DynamicData;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Cells;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Core.Headers;
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
						ICoin coin => coin.WhenAnyValue(x => x.Amount, amount => amount.ToFormattedString()),
						_ => Observable.Return("")
					};
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<SelectableCoin, Money>(model => model.Coin.Amount),
				CompareDescending = SortDescending<SelectableCoin, Money>(model => model.Coin.Amount)
			});
	}

	public static TemplateColumn<TreeNode> AnonymityScore()
	{
		return new TemplateColumn<TreeNode>(
			new AnonymityScoreHeaderViewModel(),
			new ConstantTemplate<TreeNode>(
				group => group.Value switch
				{
					ICoin coin => new AnonymityScoreCellViewModel(coin),
					_ => ""
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<SelectableCoin, int>(model => model.Coin.AnonymitySet),
				CompareDescending = SortDescending<SelectableCoin, int>(model => model.Coin.AnonymitySet)
			});
	}

	public static TemplateColumn<TreeNode> LabelsColumnForCoins()
	{
		return new TemplateColumn<TreeNode>(
			"Labels",
			new ConstantTemplate<TreeNode>(
				group =>
				{
					if (group.Value is SelectableCoin vm)
					{
						return new LabelsCellViewModel(vm.Coin.SmartLabel);
					}

					return new LabelsCellViewModel(new SmartLabel());
				}),
			GridLength.Star,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<SelectableCoin, SmartLabel>(c => c.SmartLabel),
				CompareDescending = SortDescending<SelectableCoin, SmartLabel>(c => c.SmartLabel)
			});
	}

	public static TemplateColumn<TreeNode> LabelsColumnForGroups()
	{
		return new TemplateColumn<TreeNode>(
			"Labels (Cluster)",
			new ConstantTemplate<TreeNode>(
				group => group.Value switch
				{
					CoinGroupViewModel vm => new LabelsCellViewModel(GetLabelsForGroup(vm), vm.PrivacyIndex.PrivacyLevel),
					_ => new LabelsCellViewModel(new SmartLabel())
				}),
			GridLength.Star,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<CoinGroupViewModel, PrivacyIndex>(c => c.PrivacyIndex),
				CompareDescending = SortDescending<CoinGroupViewModel, PrivacyIndex>(c => c.PrivacyIndex)
			});
	}

	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Using DisposeWith")]
	public static TemplateColumn<TreeNode> SelectionColumn(IObservable<IChangeSet<ISelectable, OutPoint>> items, IEnumerable<CommandViewModel> commands, CompositeDisposable disposables)
	{
		var selectionHeaderViewModel = new SelectionHeaderViewModel(items, i => i > 99 ? "99+" : i.ToString(), commands);
		selectionHeaderViewModel.DisposeWith(disposables);

		return new TemplateColumn<TreeNode>(
			selectionHeaderViewModel,
			new ConstantTemplate<TreeNode>(
				n => n.Value switch
				{
					CoinGroupViewModel cg => new SelectableCollectionCellViewModel(cg.Items),
					SelectableCoin coin => new CoinSelectionCellViewModel(coin),
					_ => throw new NotSupportedException()
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<SelectableCoin, bool>(model => model.IsSelected),
				CompareDescending = SortDescending<SelectableCoin, bool>(model => model.IsSelected)
			});
	}

	public static TemplateColumn<TreeNode> IndicatorsColumn()
	{
		return new TemplateColumn<TreeNode>(
			"",
			new ConstantTemplate<TreeNode>(
				n => n.Value switch
				{
					ICoin coin => new IndicatorsCellViewModel(coin),
					_ => ""
				}),
			GridLength.Auto,
			new ColumnOptions<TreeNode>
			{
				CompareAscending = SortAscending<SelectableCoin, int>(GetIndicatorPriority),
				CompareDescending = SortDescending<SelectableCoin, int>(GetIndicatorPriority)
			});
	}

	public static HierarchicalExpanderColumn<TreeNode> ChildrenColumn(TemplateColumn<TreeNode> textColumn)
	{
		return new HierarchicalExpanderColumn<TreeNode>(
			textColumn,
			group => group.Children,
			node => node.Children.Any(),
			node => node.IsExpanded);
	}

	private static IEnumerable GetLabelsForGroup(CoinGroupViewModel vm)
	{
		if (vm.Labels.IsEmpty)
		{
			return new[] { GetLabelFromPrivacyLevel(vm.PrivacyIndex.PrivacyLevel) };
		}

		return vm.Labels;
	}

	private static string GetLabelFromPrivacyLevel(PrivacyLevel privacyLevel)
	{
		return privacyLevel switch
		{
			PrivacyLevel.None => "(Invalid privacy level)",
			PrivacyLevel.SemiPrivate => "Semi-private",
			PrivacyLevel.Private => "Private",
			PrivacyLevel.NonPrivate => "Non-private",
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

	private static int GetIndicatorPriority(SelectableCoin x)
	{
		if (x.IsCoinjoining)
		{
			return 1;
		}

		if (x.BannedUntil.HasValue)
		{
			return 2;
		}

		if (!x.IsConfirmed)
		{
			return 3;
		}

		return 0;
	}
}
