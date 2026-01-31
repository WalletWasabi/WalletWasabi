using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.CoinJoinPayment;

[NavigationMetaData(Title = "Coinjoin Payments", NavigationTarget = NavigationTarget.DialogScreen)]
public partial class CoinJoinPaymentsViewModel : DialogViewModelBase<Unit>
{
	private readonly Wallet _wallet;
	private readonly IWalletModel _walletModel;
	private readonly DispatcherTimer _refreshTimer;

	[AutoNotify] private ObservableCollection<CoinJoinPaymentViewModel> _pendingPayments = new();
	[AutoNotify] private ObservableCollection<CoinJoinPaymentViewModel> _inProgressPayments = new();
	[AutoNotify] private ObservableCollection<CoinJoinPaymentViewModel> _completedPayments = new();
	[AutoNotify] private bool _hasAnyPayments;
	[AutoNotify] private bool _hasPendingPayments;
	[AutoNotify] private bool _hasInProgressPayments;
	[AutoNotify] private bool _hasCompletedPayments;
	[AutoNotify] private int _totalPaymentCount;

	private CoinJoinPaymentsViewModel(IWalletModel walletModel, Wallet wallet)
	{
		_wallet = wallet;
		_walletModel = walletModel;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
		NextCommand = ReactiveCommand.Create(() => Close());

		AddPaymentCommand = ReactiveCommand.Create(OnAddPayment);
		CancelPaymentCommand = ReactiveCommand.Create<CoinJoinPaymentViewModel>(OnCancelPayment);

		_refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
		_refreshTimer.Tick += (_, _) => RefreshPayments();
	}

	public ICommand AddPaymentCommand { get; }
	public ICommand CancelPaymentCommand { get; }

	public int MaxPaymentsPerRound => 4;

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		RefreshPayments();
		_refreshTimer.Start();

		Disposable.Create(() => _refreshTimer.Stop())
			.DisposeWith(disposables);
	}

	private void OnAddPayment()
	{
		Navigate(NavigationTarget.DialogScreen).To().AddCoinJoinPayment(_walletModel, _wallet);
	}

	private void OnCancelPayment(CoinJoinPaymentViewModel payment)
	{
		try
		{
			_wallet.BatchedPayments.AbortPayment(payment.Id);
			RefreshPayments();
		}
		catch (InvalidOperationException)
		{
			// Payment is no longer pending or was not found
			RefreshPayments();
		}
	}

	private void RefreshPayments()
	{
		var payments = _wallet.BatchedPayments.GetPayments();
		var network = _walletModel.Network;

		var pending = new ObservableCollection<CoinJoinPaymentViewModel>();
		var inProgress = new ObservableCollection<CoinJoinPaymentViewModel>();
		var completed = new ObservableCollection<CoinJoinPaymentViewModel>();

		foreach (var payment in payments)
		{
			var vm = new CoinJoinPaymentViewModel(payment, network);
			if (vm.IsPending)
			{
				pending.Add(vm);
			}
			else if (vm.IsInProgress)
			{
				inProgress.Add(vm);
			}
			else if (vm.IsFinished)
			{
				completed.Add(vm);
			}
		}

		PendingPayments = pending;
		InProgressPayments = inProgress;
		CompletedPayments = completed;

		HasPendingPayments = pending.Count > 0;
		HasInProgressPayments = inProgress.Count > 0;
		HasCompletedPayments = completed.Count > 0;
		HasAnyPayments = payments.Count > 0;
		TotalPaymentCount = payments.Count;
	}
}
