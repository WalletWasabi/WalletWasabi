using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.CoinSelection.Columns;
using WalletWasabi.Fluent.ViewModels.Wallets.Advanced.WalletCoins;
using WalletWasabi.Fluent.Views.Wallets.Advanced.WalletCoins.Columns;

namespace WalletWasabi.Fluent.ViewModels.CoinSelection;

public class CoinSelectionViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();

	public CoinSelectionViewModel(IObservable<IChangeSet<WalletCoinViewModel, int>> coinChanges)
	{
		Source = coinChanges
			.Transform(model => new TreeNode(model))
			.ObserveOn(RxApp.MainThreadScheduler)
			.ToCollection()
			.Select(CreateGridSource)
			.Replay(1)
			.RefCount();
	}

	public IObservable<FlatTreeDataGridSource<TreeNode>> Source { get; }

	public FlatTreeDataGridSource<TreeNode> CreateGridSource(IEnumerable<TreeNode> coins)
	{
		// [Column]			[View]					[Header]	[Width]		[MinWidth]		[MaxWidth]	[CanUserSort]
		// Selection		SelectionColumnView		-			Auto		-				-			false
		// Indicators		IndicatorsColumnView	-			Auto		-				-			true
		// Amount			AmountColumnView		Amount		Auto		-				-			true
		// AnonymityScore	AnonymityColumnView		<custom>	50			-				-			true
		// Labels			LabelsColumnView		Labels		*			-				-			true
		var source = new FlatTreeDataGridSource<TreeNode>(coins)
		{
			Columns =
			{
				// Selection
				SelectionColumn(),
				AmountColumn(),
				AnonymityScore(),
				LabelsColumn(),
				Address(),

				//// Indicators
				//new TemplateColumn<WalletCoinViewModel>(
				//	null,
				//	new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new IndicatorsColumnView(), true),
				//	options: new ColumnOptions<WalletCoinViewModel>
				//	{
				//		CanUserResizeColumn = false,
				//		CanUserSortColumn = true,
				//		CompareAscending = WalletCoinViewModel.SortAscending(x => GetOrderingPriority(x)),
				//		CompareDescending = WalletCoinViewModel.SortDescending(x => GetOrderingPriority(x))
				//	},
				//	width: new GridLength(0, GridUnitType.Auto)),

			//	// Amount
			//	new TemplateColumn<WalletCoinViewModel>(
			//		"Amount",
			//		new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AmountColumnView(), true),
			//		options: new ColumnOptions<WalletCoinViewModel>
			//		{
			//			CanUserResizeColumn = false,
			//			CanUserSortColumn = true,
			//			CompareAscending = WalletCoinViewModel.SortAscending(x => x.Amount),
			//			CompareDescending = WalletCoinViewModel.SortDescending(x => x.Amount)
			//		},
			//		width: new GridLength(0, GridUnitType.Auto)),

			//	// AnonymityScore
			//	new TemplateColumn<WalletCoinViewModel>(
			//		new AnonymitySetHeaderView(),
			//		new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AnonymitySetColumnView(), true),
			//		options: new ColumnOptions<WalletCoinViewModel>
			//		{
			//			CanUserResizeColumn = false,
			//			CanUserSortColumn = true,
			//			CompareAscending = WalletCoinViewModel.SortAscending(x => x.AnonymitySet),
			//			CompareDescending = WalletCoinViewModel.SortDescending(x => x.AnonymitySet)
			//		},
			//		width: new GridLength(50, GridUnitType.Pixel)),

			//	// Labels
			//	new TemplateColumn<WalletCoinViewModel>(
			//		"Labels",
			//		new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new LabelsColumnView(), true),
			//		options: new ColumnOptions<WalletCoinViewModel>
			//		{
			//			CanUserResizeColumn = false,
			//			CanUserSortColumn = true,
			//			CompareAscending = WalletCoinViewModel.SortAscending(x => x.SmartLabel),
			//			CompareDescending = WalletCoinViewModel.SortDescending(x => x.SmartLabel)
			//		},
			//		width: new GridLength(1, GridUnitType.Star)),

			//	// Address
			//	new TemplateColumn<WalletCoinViewModel>(
			//		"Address",
			//		new FuncDataTemplate<WalletCoinViewModel>((node, ns) => new AddressColumnView(), true),
			//		options: new ColumnOptions<WalletCoinViewModel>
			//		{
			//			CanUserResizeColumn = false,
			//			CanUserSortColumn = true,
			//			CompareAscending = WalletCoinViewModel.SortAscending(x => x.Address),
			//			CompareDescending = WalletCoinViewModel.SortDescending(x => x.Address)
			//		},
			//		width: new GridLength(1, GridUnitType.Star))
			}
		};

		source.RowSelection!.SingleSelect = true;

		return source;
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

	private static TemplateColumn<TreeNode> AnonymityScore()
	{
		return new TemplateColumn<TreeNode>(
			"Anonymity Score",
			new ObservableTemplate<TreeNode, int>(
				group =>
				{
					return group.Value switch
					{
						WalletCoinViewModel coin => new BehaviorSubject<int>(coin.AnonymitySet),
						_ => throw new NotSupportedException(),
					};
				}));
	}

	private static TemplateColumn<TreeNode> Address()
	{
		return new TemplateColumn<TreeNode>(
			"Anonymity Score",
			new ObservableTemplate<TreeNode, string>(
				group =>
				{
					return group.Value switch
					{
						WalletCoinViewModel coin => new BehaviorSubject<string>(coin.Address ?? ""),
						_ => throw new NotSupportedException(),
					};
				}));
	}

	private static TemplateColumn<TreeNode> LabelsColumn()
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

	private TemplateColumn<TreeNode> SelectionColumn()
	{
		return new TemplateColumn<TreeNode>(
			"",
			new ConstantTemplate<TreeNode>(
				n =>
				{
					var selectable = (ISelectable)n.Value;
					var isSelectedViewModel = new IsSelectedViewModel(selectable);
					_disposables.Add(isSelectedViewModel);
					return isSelectedViewModel;
				}));
	}

	private static int GetOrderingPriority(WalletCoinViewModel x)
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

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
