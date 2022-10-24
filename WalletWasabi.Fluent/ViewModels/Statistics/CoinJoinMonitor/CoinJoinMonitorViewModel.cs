using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using DynamicData;
using DynamicData.Binding;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Fluent.Views.Statistics.CoinJoinMonitor.Columns;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Statistics.CoinJoinMonitor;

[NavigationMetaData(
	Title = "Coinjoin Monitor",
	Caption = "Displays coinjoin monitoring",
	IconName = "wallet_action_coinjoin",
	Order = 5,
	Category = "General",
	Keywords = new[] { "CoinJoin", "Monitor", "Arena", "WabiSabi", "Coordinator", "Statistics", },
	NavBarPosition = NavBarPosition.None,
	NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinMonitorViewModel : RoutableViewModel
{
	private readonly SourceList<RoundStateViewModel> _roundStatesList;
	private readonly ObservableCollectionExtended<RoundStateViewModel> _roundStates;
	[AutoNotify] private RoundStateViewModel? _selectedItem;

	public CoinJoinMonitorViewModel()
	{
		_roundStatesList = new SourceList<RoundStateViewModel>();
		_roundStates = new ObservableCollectionExtended<RoundStateViewModel>();

		EnableBack = false;

		NextCommand = CancelCommand;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		_roundStatesList
			.Connect()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Sort(SortExpressionComparer<RoundStateViewModel>
				.Ascending(x => x.Phase))
			.Bind(_roundStates)
			.Subscribe();

		Source = CreateRoundStateSource();

		Source.RowSelection!.SingleSelect = true;

		Source.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(x => SelectedItem = x);
	}

	public ObservableCollection<RoundStateViewModel> RoundStates => _roundStates;

	public FlatTreeDataGridSource<RoundStateViewModel> Source { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ => Update())
			.DisposeWith(disposables);
	}

	private void Update()
	{
		var selectedItemId = SelectedItem?.Id;
		var newRoundStatesDict = GenerateRoundStatesList().ToDictionary(x => x.Id);

		_roundStatesList.Edit(roundStates =>
		{
			foreach (var roundState in roundStates)
			{
				if (roundState.Id is { } && !newRoundStatesDict.ContainsKey(roundState.Id))
				{
					roundStates.Remove(roundState);
				}
			}

			foreach (var roundState in roundStates)
			{
				if (roundState.Id is { })
				{
					roundState.Update(newRoundStatesDict[roundState.Id]);
				}
			}

			var existing = roundStates
				.Where(x => x.Id is not null)
				.ToDictionary<RoundStateViewModel, uint256>(x => x.Id!);

			foreach (var newRoundState in newRoundStatesDict)
			{
				if (!existing.ContainsKey(newRoundState.Key))
				{
					roundStates.Add(new RoundStateViewModel(newRoundState.Value));
				}
			}
		});

		var selectedItem = _roundStatesList.Items.FirstOrDefault(x => x.Id == selectedItemId);
		if (selectedItem is { })
		{
			SelectedItem = selectedItem;
		}
	}

	private FlatTreeDataGridSource<RoundStateViewModel> CreateRoundStateSource()
	{
		return new FlatTreeDataGridSource<RoundStateViewModel>(_roundStates)
		{
			Columns =
			{
				// Id
				new TemplateColumn<RoundStateViewModel>(
					"Id",
					new FuncDataTemplate<RoundStateViewModel>((_, _) => new IdColumnView(), true),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.Id),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.Id),
						MinWidth = new GridLength(100, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// IsBlameRound
				new TemplateColumn<RoundStateViewModel>(
					"Blame",
					new FuncDataTemplate<RoundStateViewModel>((_, _) => new IsBlameRoundColumnView(), true),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.IsBlameRound),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.IsBlameRound),
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// InputCount
				new TemplateColumn<RoundStateViewModel>(
					"InputCount",
					new FuncDataTemplate<RoundStateViewModel>((_, _) => new InputCountColumnView(), true),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.InputCount),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.InputCount),
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// MaxSuggestedAmount
				new TemplateColumn<RoundStateViewModel>(
					"MaxSuggestedAmount",
					new FuncDataTemplate<RoundStateViewModel>((_, _) => new InputCountColumnView(), true),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.MaxSuggestedAmount),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.MaxSuggestedAmount),
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// InputRegistrationRemaining
				new TemplateColumn<RoundStateViewModel>(
					"Remaining",
					new FuncDataTemplate<RoundStateViewModel>((_, _) => new InputRegistrationRemainingColumnView(), true),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.InputRegistrationRemaining),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.InputRegistrationRemaining),
					},
					width: new GridLength(0, GridUnitType.Auto)),

				// Phase
				new TemplateColumn<RoundStateViewModel>(
					"Phase",
					new FuncDataTemplate<RoundStateViewModel>((_, _) => new PhaseColumnView(), true),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.Phase),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.Phase),
					},
					width: new GridLength(0, GridUnitType.Auto)),
			}
		};
	}

	private IEnumerable<RoundState> GenerateRoundStatesList()
	{
		var coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();
		if (coinJoinManager?.RoundStatusUpdater is { } roundStateUpdater)
		{
			foreach (var roundState in roundStateUpdater.GetRoundStates())
			{
				yield return roundState;
			}
		}
	}
}
