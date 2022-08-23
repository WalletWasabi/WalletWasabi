using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public partial class LabelBasedCoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	[AutoNotify] private string _filter = "";

	public LabelBasedCoinSelectionViewModel(IObservable<IChangeSet<WalletCoinViewModel, int>> coinChanges)
	{
		var filterPredicate = this
			.WhenAnyValue(x => x.Filter)
			.Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler)
			.DistinctUntilChanged()
			.Select(SearchItemFilterFunc);

		coinChanges
			.Group(x => x.SmartLabel)
			.TransformWithInlineUpdate(
				group =>
				{
					var coinGroup = new CoinGroupViewModel(group.Key, group.Cache);
					return new TreeNode(coinGroup, coinGroup.Items.Select(x => new TreeNode(x)));
				})
			.Filter(filterPredicate)
			//.DisposeMany()	// Disposal behavior of Filter is unwanted
			.ObserveOn(RxApp.MainThreadScheduler)
			.Bind(out var items)
			.Subscribe()
			.DisposeWith(_disposables);

		Source = CreateGridSource(items);
	}

	public HierarchicalTreeDataGridSource<TreeNode> Source { get; }

	public void Dispose()
	{
		_disposables.Dispose();
		Source.Dispose();
	}

	private static Func<TreeNode, bool> SearchItemFilterFunc(string? text)
	{
		return tn =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return true;
			}

			if (tn.Value is CoinGroupViewModel cg)
			{
				var containsLabel = cg.Labels.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
				return containsLabel;
			}

			return false;
		};
	}

	private static TemplateColumn<TreeNode> LabelsColumn()
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

	private static TemplateColumn<TreeNode> AmountColumn()
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

	private static HierarchicalExpanderColumn<TreeNode> ChildrenColumn()
	{
		return new HierarchicalExpanderColumn<TreeNode>(
			new TextColumn<TreeNode, string>("", group => ""),
			group => group.Children,
			node => node.Children.Any());
	}

	private HierarchicalTreeDataGridSource<TreeNode> CreateGridSource(IEnumerable<TreeNode> groups)
	{
		var source = new HierarchicalTreeDataGridSource<TreeNode>(groups)
		{
			Columns =
			{
				SelectionColumn(),
				ChildrenColumn(),
				AmountColumn(),
				LabelsColumn()
			}
		};

		source.RowSelection!.SingleSelect = true;

		return source;
	}

	private TemplateColumn<TreeNode> SelectionColumn()
	{
		return new TemplateColumn<TreeNode>(
			"",
			new ConstantTemplate<TreeNode>(
				n =>
				{
					var selectable = (ISelectable) n.Value;
					var isSelectedViewModel = new IsSelectedViewModel(selectable);
					_disposables.Add(isSelectedViewModel);
					return isSelectedViewModel;
				}));
	}
}
