using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Fluent.TreeDataGrid;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;

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
				.Descending(x => x.InputRegistrationStart))
			.Bind(_roundStates)
			.Subscribe();

		Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				var newRoundStatesList = GenerateRoundStatesList().ToArray();

				_roundStatesList.Edit(x =>
				{
					x.Clear();
					x.AddRange(newRoundStatesList);
				});
			});

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
						MinWidth = new GridLength(100, GridUnitType.Pixel)
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

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
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),

				// EndRoundState
				new PrivacyTextColumn<RoundStateViewModel>(
					"EndRoundState",
					x => x.EndRoundState.ToString(),
					options: new ColumnOptions<RoundStateViewModel>
					{
						CanUserResizeColumn = false,
						CanUserSortColumn = true,
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
					},
					width: new GridLength(0, GridUnitType.Auto),
					numberOfPrivacyChars: 9),
			}
		};

		Source.RowSelection!.SingleSelect = true;

		Source.RowSelection
			.WhenAnyValue(x => x.SelectedItem)
			.Subscribe(x => SelectedItem = x);
	}

	public ObservableCollection<RoundStateViewModel> RoundStates => _roundStates;

	public FlatTreeDataGridSource<RoundStateViewModel> Source { get; }

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
