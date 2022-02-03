using System.Reactive.Linq;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletSettingsViewModel : RoutableViewModel
{
	[AutoNotify] private bool _preferPsbtWorkflow;
	[AutoNotify] private bool _autoCoinJoin;
	[AutoNotify] private int _minAnonScoreTarget;
	[AutoNotify] private int _maxAnonScoreTarget;

	public WalletSettingsViewModel(WalletViewModelBase walletViewModelBase)
	{
		var wallet = walletViewModelBase.Wallet;
		Title = $"{wallet.WalletName} - Wallet Settings";
		_preferPsbtWorkflow = wallet.KeyManager.PreferPsbtWorkflow;
		_autoCoinJoin = wallet.KeyManager.AutoCoinJoin;
		IsHardwareWallet = wallet.KeyManager.IsHardwareWallet;
		IsWatchOnly = wallet.KeyManager.IsWatchOnly;

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
			.Subscribe(_ =>
			{
				wallet.KeyManager.SetAnonScoreTargets(MinAnonScoreTarget, MaxAnonScoreTarget);
			});

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
	}

	public bool IsHardwareWallet { get; }

	public bool IsWatchOnly { get; }

	public override sealed string Title { get; protected set; }

	public ICommand VerifyRecoveryWordsCommand { get; }
}
