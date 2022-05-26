using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.CoinJoinProfiles;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private int _anonScoreTarget;
	[AutoNotify] private string _plebStopThreshold;

	private Wallet _wallet;

	public WalletSettingsViewModel(WalletViewModelBase walletViewModelBase)
	{
		_wallet = walletViewModelBase.Wallet;
		Title = $"{_wallet.WalletName} - Wallet Settings";
		_preferPsbtWorkflow = _wallet.KeyManager.PreferPsbtWorkflow;
		_autoCoinJoin = _wallet.KeyManager.AutoCoinJoin;
		IsHardwareWallet = _wallet.KeyManager.IsHardwareWallet;
		IsWatchOnly = _wallet.KeyManager.IsWatchOnly;
		_plebStopThreshold = _wallet.KeyManager.PlebStopThreshold?.ToString() ?? KeyManager.DefaultPlebStopThreshold.ToString();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To(new VerifyRecoveryWordsViewModel(_wallet)));

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(value =>
			{
				_wallet.KeyManager.PreferPsbtWorkflow = value;
				_wallet.KeyManager.ToFile();
				walletViewModelBase.RaisePropertyChanged(nameof(walletViewModelBase.PreferPsbtWorkflow));
			});

		SetAutoCoinJoin = ReactiveCommand.CreateFromTask(async () =>
		{
			if (!_wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				await NavigateDialogAsync(new CoinJoinProfilesViewModel(_wallet.KeyManager, false), NavigationTarget.DialogScreen);
			}

			if (_wallet.KeyManager.IsCoinjoinProfileSelected)
			{
				AutoCoinJoin = !AutoCoinJoin;
				_wallet.KeyManager.AutoCoinJoin = AutoCoinJoin;
				_wallet.KeyManager.ToFile();
			}
			else
			{
				AutoCoinJoin = false;
			}
		});

		_anonScoreTarget = _wallet.KeyManager.AnonScoreTarget;

		this.WhenAnyValue(x => x.AnonScoreTarget)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Throttle(TimeSpan.FromMilliseconds(1000))
			.Skip(1)
			.Subscribe(_ => _wallet.KeyManager.SetAnonScoreTarget(AnonScoreTarget));

		this.ValidateProperty(x => x.PlebStopThreshold, ValidatePlebStopThreshold);

		this.WhenAnyValue(x => x.PlebStopThreshold)
			.Skip(1)
			.Throttle(TimeSpan.FromMilliseconds(1000))
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Subscribe(x =>
			{
				if (Money.TryParse(x, out Money result) && result != _wallet.KeyManager.PlebStopThreshold)
				{
					_wallet.KeyManager.PlebStopThreshold = result;
					_wallet.KeyManager.ToFile();
				}
			});
	}

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public override sealed string Title { get; protected set; }

	public ICommand SetAutoCoinJoin { get; }

	public ICommand VerifyRecoveryWordsCommand { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);
		PlebStopThreshold = _wallet.KeyManager.PlebStopThreshold.ToString();
		AnonScoreTarget = _wallet.KeyManager.AnonScoreTarget;
	}

	private void ValidatePlebStopThreshold(IValidationErrors errors) =>
		ValidatePlebStopThreshold(errors, PlebStopThreshold);

	private static void ValidatePlebStopThreshold(IValidationErrors errors, string plebStopThreshold)
	{
		if (string.IsNullOrWhiteSpace(plebStopThreshold) || string.IsNullOrEmpty(plebStopThreshold))
		{
			return;
		}

		if (plebStopThreshold.Contains(',', StringComparison.InvariantCultureIgnoreCase))
		{
			errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
		}
		else if (!decimal.TryParse(plebStopThreshold, out _))
		{
			errors.Add(ErrorSeverity.Error, "Invalid coinjoin threshold.");
		}
	}
}
