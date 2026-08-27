using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using DynamicData;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.CoinJoinPayment;
using WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;
using WalletWasabi.Fluent.ViewModels.Wallets.Settings;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Coinjoins;

[NavigationMetaData(Title = "Coinjoin", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinDialogViewModel : DialogViewModelBase<Unit>
{
	private readonly IWalletModel _walletModel;
	private readonly Wallet _wallet;
	private readonly CoinJoinStateViewModel? _coinJoinState;
	private readonly DispatcherTimer _refreshTimer;

	[AutoNotify] private ObservableCollection<CoinJoinPaymentViewModel> _payments = new();
	[AutoNotify] private bool _hasPayments;
	[AutoNotify] private string _startStopContent = StartContent;
	[AutoNotify] private bool _isStartAction = true;
	[AutoNotify] private string _coinSelectionText = "";

	private const string StartContent = "Start coinjoin";
	private const string StopContent = "Stop coinjoin";
	private const string ContinueContent = "Continue anyway";

	public CoinJoinDialogViewModel(
		UiContext uiContext,
		IWalletModel walletModel,
		Wallet wallet,
		CoinJoinStateViewModel? coinJoinState,
		WalletCoinJoinSettingsViewModel settings) : base(uiContext)
	{
		_walletModel = walletModel;
		_wallet = wallet;
		_coinJoinState = coinJoinState;

		Settings = settings;
		Privacy = new PrivacyControlTileViewModel(uiContext, walletModel);

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);


		if (coinJoinState is not null)
		{
			var isStartAction = coinJoinState.WhenAnyValue(
				x => x.PlayVisible,
				x => x.IsCoinjoinActive,
				(playVisible, isActive) => playVisible || !isActive);

			isStartAction.BindTo(this, x => x.IsStartAction);

			coinJoinState.WhenAnyValue(x => x.IsPlebStopActive)
				.CombineLatest(isStartAction, (isPlebStopActive, isStart) =>
					isPlebStopActive ? ContinueContent :
					isStart ? StartContent : StopContent)
				.BindTo(this, x => x.StartStopContent);

			var canStart = coinJoinState.WhenAnyValue(x => x.PlayVisible, x => x.PlayEnabled, (visible, enabled) => visible && enabled);

			var canStop = coinJoinState.WhenAnyValue(
				x => x.IsInCriticalPhase,
				x => x.PauseSpreading,
				(isInCriticalPhase, pauseSpreading) => !isInCriticalPhase && !pauseSpreading);

			var canStartOrStop = isStartAction.CombineLatest(canStart, canStop, (isStart, start, stop) => isStart ? start : stop);

			NextCommand = ReactiveCommand.Create(StartOrStop, canStartOrStop);
		}

		NavigateToCoordinatorSettingsCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			if (UiContext.MainViewModel is { } mainViewModel)
			{
				await mainViewModel.SettingsPage.ActivateCoordinatorTabAsync();
			}
		});

		AddPaymentCommand = ReactiveCommand.Create(
			() => Navigate(NavigationTarget.DialogScreen).To().AddCoinJoinPayment(_walletModel, _wallet));

		CancelPaymentCommand = ReactiveCommand.Create<CoinJoinPaymentViewModel>(OnCancelPayment);

		SelectCoinsCommand = ReactiveCommand.CreateFromTask(
			async () => await NavigateDialogAsync(new CoinjoinCoinSelectionViewModel(UiContext, _walletModel), NavigationTarget.DialogScreen));

		_refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
		_refreshTimer.Tick += (_, _) => Refresh();
	}

	public PrivacyControlTileViewModel Privacy { get; }

	public WalletCoinJoinSettingsViewModel Settings { get; }

	public CoinJoinStateViewModel? CoinJoinState => _coinJoinState;

	public bool IsCoinjoinAvailable => _coinJoinState is not null;

	public ICommand AddPaymentCommand { get; }

	public ICommand CancelPaymentCommand { get; }

	public ICommand SelectCoinsCommand { get; }

	public ICommand NavigateToCoordinatorSettingsCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Privacy.Activate(disposables);

		if (!IsCoinjoinAvailable)
		{
			return;
		}

		Refresh();
		_refreshTimer.Start();

		UiContext.Services.EventBus.AsObservable<PaymentBatchChanged>()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(_ => Refresh())
			.Subscribe()
			.DisposeWith(disposables);

		_walletModel.Coins.List
			.Connect(suppressEmptyChangeSets: false)
			.ToCollection()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(coins => UpdateCoinSelectionText(coins.ToArray()))
			.Subscribe()
			.DisposeWith(disposables);

// Dispose objects before losing scope
#pragma warning disable CA2000
		Disposable.Create(() => _refreshTimer.Stop())
			.DisposeWith(disposables);
#pragma warning restore CA2000
	}

	private void StartOrStop()
	{
		if (_coinJoinState is null)
		{
			return;
		}

		if (IsStartAction)
		{
			_coinJoinState.PlayCommand.ExecuteIfCan();
		}
		else
		{
			_coinJoinState.StopPauseCommand.ExecuteIfCan();
		}

		Close();
	}

	private void OnCancelPayment(CoinJoinPaymentViewModel payment)
	{
		try
		{
			_wallet.CancelCoinJoinPayment(payment.Id);
		}
		catch (InvalidOperationException)
		{
			// Payment not found, ignore
		}

		Refresh();
	}

	private void Refresh()
	{
		var network = _walletModel.Network;

		var payments = _wallet.BatchedPayments.GetPayments()
			.Where(x => x.State is not FinishedPayment)
			.Select(x => new CoinJoinPaymentViewModel(UiContext, x, network))
			.ToList();

		var isUnchanged =
			payments.Count == Payments.Count &&
			payments.Zip(Payments).All(pair => pair.First.Id == pair.Second.Id && pair.First.Status == pair.Second.Status);

		if (isUnchanged)
		{
			return;
		}

		Payments = new ObservableCollection<CoinJoinPaymentViewModel>(payments);
		HasPayments = Payments.Count > 0;
	}

	private void UpdateCoinSelectionText(CoinModel[] coins)
	{
		var excluded = coins.Count(x => x.IsExcludedFromCoinJoin);

		CoinSelectionText =
			excluded == 0
			? $"All {coins.Length} coins"
			: $"{coins.Length - excluded} of {coins.Length} coins";
	}
}
