using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Client;

namespace WalletWasabi.Fluent.ViewModels.Statistics;

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

		Source = new FlatTreeDataGridSource<RoundStateViewModel>(_roundStates)
		{
			Columns =
			{
				// Id
				new PrivacyTextColumn<RoundStateViewModel>(
					"Id",
					x =>
					{
						var id = x.Id.ToString();
						return id.Substring(Math.Max(0, id.Length - 6));
					},
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.Id),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.Id),
						MinWidth = new GridLength(100, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// IsBlameRound
				new PrivacyTextColumn<RoundStateViewModel>(
					"IsBlameRound",
					x => x.IsBlameRound.ToString(),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.IsBlameRound),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.IsBlameRound),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// InputCount
				new PrivacyTextColumn<RoundStateViewModel>(
					"InputCount",
					x => x.InputCount.ToString(),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.InputCount),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.InputCount),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// MaxSuggestedAmount
				new PrivacyTextColumn<RoundStateViewModel>(
					"MaxSuggestedAmount",
					x => x.MaxSuggestedAmount.ToString(),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.MaxSuggestedAmount),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.MaxSuggestedAmount),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// InputRegistrationRemaining
				new PrivacyTextColumn<RoundStateViewModel>(
					"InputRegistrationRemaining",
					x => $"{x.InputRegistrationRemaining:MM/dd/yyyy HH:mm}",
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.InputRegistrationRemaining),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.InputRegistrationRemaining),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// Phase
				new PrivacyTextColumn<RoundStateViewModel>(
					"Phase",
					x => x.Phase.ToString(),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.Phase),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.Phase),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

/*
				// BlameOf
				new PrivacyTextColumn<RoundStateViewModel>(
					"BlameOf",
					x =>
					{
						var blameOf = x.BlameOf.ToString();
						return blameOf.Substring(Math.Max(0, blameOf.Length - 6));
					},
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.BlameOf),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.BlameOf),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),
*/
/*
				// EndRoundState
				new PrivacyTextColumn<RoundStateViewModel>(
					"EndRoundState",
					x => x.EndRoundState.ToString(),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.EndRoundState),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.EndRoundState),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// InputRegistrationStart
				new PrivacyTextColumn<RoundStateViewModel>(
					"InputRegistrationStart",
					x => $"{x.InputRegistrationStart.ToLocalTime():MM/dd/yyyy HH:mm}",
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.InputRegistrationStart),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.InputRegistrationStart),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// InputRegistrationTimeout
				new PrivacyTextColumn<RoundStateViewModel>(
					"InputRegistrationTimeout",
					x => x.InputRegistrationTimeout.ToString(),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
						CompareAscending = RoundStateViewModel.SortAscending(x => x.InputRegistrationTimeout),
						CompareDescending = RoundStateViewModel.SortDescending(x => x.InputRegistrationTimeout),
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),
*/
			}
		};

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
		var newRoundStatesList = GenerateRoundStatesList().ToArray();

		_roundStatesList.Edit(x =>
		{
			x.Clear();
			x.AddRange(newRoundStatesList);
		});

		var selectedItem = newRoundStatesList.FirstOrDefault(x => x.Id == selectedItemId);
		if (selectedItem is { })
		{
			SelectedItem = selectedItem;
		}
	}

	private IEnumerable<RoundStateViewModel> GenerateRoundStatesList()
	{
		var coinJoinManager = Services.HostedServices.GetOrDefault<CoinJoinManager>();
		if (coinJoinManager?.RoundStatusUpdater is { } roundStateUpdater)
		{
			foreach (var roundState in roundStateUpdater.GetRoundStates())
			{
				yield return new RoundStateViewModel(roundState);
			}
		}
	}
}
