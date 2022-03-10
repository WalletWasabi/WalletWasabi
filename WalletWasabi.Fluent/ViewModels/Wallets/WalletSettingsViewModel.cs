using System.Reactive.Linq;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Models;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private int _minAnonScoreTarget;
	[AutoNotify] private int _maxAnonScoreTarget;
	[AutoNotify] private string _plebStopThreshold;

	public WalletSettingsViewModel(WalletViewModelBase walletViewModelBase)
	{
		var wallet = walletViewModelBase.Wallet;
		Title = $"{wallet.WalletName} - Wallet Settings";
		_preferPsbtWorkflow = wallet.KeyManager.PreferPsbtWorkflow;
		_autoCoinJoin = wallet.KeyManager.AutoCoinJoin;
		IsHardwareWallet = wallet.KeyManager.IsHardwareWallet;
		IsWatchOnly = wallet.KeyManager.IsWatchOnly;
		_plebStopThreshold = wallet.KeyManager.PlebStopThreshold.ToString();

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		NextCommand = CancelCommand;

		VerifyRecoveryWordsCommand = ReactiveCommand.Create(() => Navigate().To(new VerifyRecoveryWordsViewModel(wallet)));

		this.WhenAnyValue(x => x.PreferPsbtWorkflow)
			.Skip(1)
			.Subscribe(value =>
			{
				wallet.KeyManager.PreferPsbtWorkflow = value;
				wallet.KeyManager.ToFile();
				walletViewModelBase.RaisePropertyChanged(nameof(walletViewModelBase.PreferPsbtWorkflow));
			});

		this.WhenAnyValue(x => x.AutoCoinJoin)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Skip(1)
			.Subscribe(x =>
			{
				wallet.KeyManager.AutoCoinJoin = x;
				wallet.KeyManager.ToFile();
			});

		_minAnonScoreTarget = wallet.KeyManager.MinAnonScoreTarget;
		_maxAnonScoreTarget = wallet.KeyManager.MaxAnonScoreTarget;

		this.WhenAnyValue(
				x => x.MinAnonScoreTarget,
				x => x.MaxAnonScoreTarget)
			.ObserveOn(RxApp.TaskpoolScheduler)
			.Throttle(TimeSpan.FromMilliseconds(1000))
			.Skip(1)
			.Subscribe(_ => wallet.KeyManager.SetAnonScoreTargets(MinAnonScoreTarget, MaxAnonScoreTarget));

		this.WhenAnyValue(x => x.MinAnonScoreTarget)
			.Subscribe(
				x =>
				{
					if (x >= MaxAnonScoreTarget)
					{
						MaxAnonScoreTarget = x + 1;
					}
				});

		this.WhenAnyValue(x => x.MaxAnonScoreTarget)
			.Subscribe(
				x =>
				{
					if (x <= MinAnonScoreTarget)
					{
						MinAnonScoreTarget = x - 1;
					}
				});

		this.ValidateProperty(x => x.PlebStopThreshold, ValidatePlebStopThreshold);
		this.WhenAnyValue(x => x.PlebStopThreshold)
			.Subscribe(
			x =>
			{
				if (Money.TryParse(x, out Money result))
				{
					wallet.KeyManager.PlebStopThreshold = result;
					wallet.KeyManager.ToFile();
				}
			});
	}

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public override sealed string Title { get; protected set; }

	public ICommand VerifyRecoveryWordsCommand { get; }

	private void ValidatePlebStopThreshold(IValidationErrors errors) =>
		ValidatePlebStopThreshold(errors, PlebStopThreshold);

	private static void ValidatePlebStopThreshold(IValidationErrors errors, string plebStopThreshold)
	{
		if (!string.IsNullOrWhiteSpace(plebStopThreshold))
		{
			return;
		}

		if (!string.IsNullOrEmpty(plebStopThreshold) && plebStopThreshold.Contains(
			',',
			StringComparison.InvariantCultureIgnoreCase))
		{
			errors.Add(ErrorSeverity.Error, "Use decimal point instead of comma.");
		}

		if (!decimal.TryParse(plebStopThreshold, out _))
		{
			errors.Add(ErrorSeverity.Error, "Invalid coinjoin threshold.");
		}
	}
}
