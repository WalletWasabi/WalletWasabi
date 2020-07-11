using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.CoinJoin.Client.Rounds;
using WalletWasabi.CoinJoin.Common.Models;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Validation;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Wallets;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WasabiDocumentTabViewModel, IWalletViewModel
	{
		private const string EnqueueButtonTextString = "Enqueue Selected Coins";
		private const string EnqueuingButtonTextString = "Queuing coins...";
		private const string DequeueButtonTextString = "Dequeue Selected Coins";
		private const string DequeuingButtonTextString = "Dequeuing coins...";

		private long _roundId;
		private RoundPhaseState _roundPhaseState;
		private DateTimeOffset _roundTimesout;
		private TimeSpan _timeLeftTillRoundTimeout;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		private int _peersRegistered;
		private int _peersNeeded;
		private string _password;
		private Money _amountQueued;
		private bool _isEnqueueBusy;
		private bool _isDequeueBusy;
		private string _enqueueButtonText;
		private string _dequeueButtonText;
		private string _coinJoinUntilAnonymitySet;

		public CoinJoinTabViewModel(Wallet wallet)
			: base("CoinJoin")
		{
			Global = Locator.Current.GetService<Global>();
			Wallet = wallet;

			this.ValidateProperty(x => x.Password, ValidatePassword);

			Password = "";
			TimeLeftTillRoundTimeout = TimeSpan.Zero;

			CoinsList = new CoinListViewModel(Wallet, canDequeueCoins: true);

			Observable
				.FromEventPattern<SmartCoin>(CoinsList, nameof(CoinsList.DequeueCoinsPressed))
				.Subscribe(async x => await DoDequeueAsync(x.EventArgs));

			AmountQueued = Money.Zero; // Global.ChaumianClient.State.SumAllQueuedCoinAmounts();

			EnqueueCommand = ReactiveCommand.CreateFromTask(async () => await DoEnqueueAsync(CoinsList.Coins.Where(c => c.IsSelected).Select(c => c.Model)));

			DequeueCommand = ReactiveCommand.CreateFromTask(async () => await DoDequeueAsync(CoinsList.Coins.Where(c => c.IsSelected).Select(x => x.Model)));

			PrivacySomeCommand = ReactiveCommand.Create(() => CoinJoinUntilAnonymitySet = MixUntilAnonymitySet.PrivacyLevelSome.ToString());

			PrivacyFineCommand = ReactiveCommand.Create(() => CoinJoinUntilAnonymitySet = MixUntilAnonymitySet.PrivacyLevelFine.ToString());

			PrivacyStrongCommand = ReactiveCommand.Create(() => CoinJoinUntilAnonymitySet = MixUntilAnonymitySet.PrivacyLevelStrong.ToString());

			TargetButtonCommand = ReactiveCommand.Create(() =>
			   {
				   switch (CoinJoinUntilAnonymitySet)
				   {
					   case nameof(MixUntilAnonymitySet.PrivacyLevelSome):
						   CoinJoinUntilAnonymitySet = MixUntilAnonymitySet.PrivacyLevelFine.ToString();
						   break;

					   case nameof(MixUntilAnonymitySet.PrivacyLevelFine):
						   CoinJoinUntilAnonymitySet = MixUntilAnonymitySet.PrivacyLevelStrong.ToString();
						   break;

					   case nameof(MixUntilAnonymitySet.PrivacyLevelStrong):
						   CoinJoinUntilAnonymitySet = MixUntilAnonymitySet.PrivacyLevelSome.ToString();
						   break;
				   }

				   Global.Config.MixUntilAnonymitySet = CoinJoinUntilAnonymitySet;

				   // Config.json can be different than Global.Config. Only change the MixUntilAnonymitySet in the file.
				   var config = new Config(Global.Config.FilePath);
				   config.LoadOrCreateDefaultFile();
				   config.MixUntilAnonymitySet = CoinJoinUntilAnonymitySet;
				   config.ToFile();
			   });

			this.WhenAnyValue(x => x.IsEnqueueBusy)
				.Select(x => x ? EnqueuingButtonTextString : EnqueueButtonTextString)
				.Subscribe(text => EnqueueButtonText = text);

			this.WhenAnyValue(x => x.IsDequeueBusy)
				.Select(x => x ? DequeuingButtonTextString : DequeueButtonTextString)
				.Subscribe(text => DequeueButtonText = text);

			this.WhenAnyValue(x => x.RoundTimesout)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					TimeSpan left = x - DateTimeOffset.UtcNow;
					TimeLeftTillRoundTimeout = left > TimeSpan.Zero ? left : TimeSpan.Zero; // Make sure cannot be less than zero.
				});

			Observable
				.Merge(EnqueueCommand.ThrownExceptions)
				.Merge(DequeueCommand.ThrownExceptions)
				.Merge(PrivacySomeCommand.ThrownExceptions)
				.Merge(PrivacyFineCommand.ThrownExceptions)
				.Merge(PrivacyStrongCommand.ThrownExceptions)
				.Merge(TargetButtonCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex => Logger.LogError(ex));
		}

		private Global Global { get; }

		private Wallet Wallet { get; }

		Wallet IWalletViewModel.Wallet => Wallet;

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public CoinListViewModel CoinsList { get; }

		public Money AmountQueued
		{
			get => _amountQueued;
			set => this.RaiseAndSetIfChanged(ref _amountQueued, value);
		}

		public long RoundId
		{
			get => _roundId;
			set => this.RaiseAndSetIfChanged(ref _roundId, value);
		}

		public RoundPhaseState RoundPhaseState
		{
			get => _roundPhaseState;
			set => this.RaiseAndSetIfChanged(ref _roundPhaseState, value);
		}

		public DateTimeOffset RoundTimesout
		{
			get => _roundTimesout;
			set => this.RaiseAndSetIfChanged(ref _roundTimesout, value);
		}

		public TimeSpan TimeLeftTillRoundTimeout
		{
			get => _timeLeftTillRoundTimeout;
			set => this.RaiseAndSetIfChanged(ref _timeLeftTillRoundTimeout, value);
		}

		public Money RequiredBTC
		{
			get => _requiredBTC;
			set => this.RaiseAndSetIfChanged(ref _requiredBTC, value);
		}

		public string CoordinatorFeePercent
		{
			get => _coordinatorFeePercent;
			set => this.RaiseAndSetIfChanged(ref _coordinatorFeePercent, value);
		}

		public int PeersRegistered
		{
			get => _peersRegistered;
			set => this.RaiseAndSetIfChanged(ref _peersRegistered, value);
		}

		public int PeersNeeded
		{
			get => _peersNeeded;
			set => this.RaiseAndSetIfChanged(ref _peersNeeded, value);
		}

		public bool IsEnqueueBusy
		{
			get => _isEnqueueBusy;
			set => this.RaiseAndSetIfChanged(ref _isEnqueueBusy, value);
		}

		public bool IsDequeueBusy
		{
			get => _isDequeueBusy;
			set => this.RaiseAndSetIfChanged(ref _isDequeueBusy, value);
		}

		public string EnqueueButtonText
		{
			get => _enqueueButtonText;
			set => this.RaiseAndSetIfChanged(ref _enqueueButtonText, value);
		}

		public string DequeueButtonText
		{
			get => _dequeueButtonText;
			set => this.RaiseAndSetIfChanged(ref _dequeueButtonText, value);
		}

		public string CoinJoinUntilAnonymitySet
		{
			get => _coinJoinUntilAnonymitySet;
			set => this.RaiseAndSetIfChanged(ref _coinJoinUntilAnonymitySet, value);
		}

		public bool IsWatchOnly => Wallet.KeyManager.IsWatchOnly;
		public bool IsHardwareWallet => Wallet.KeyManager.IsHardwareWallet;

		public bool IsLurkingWifeMode => Global.UiConfig.LurkingWifeMode;

		public ReactiveCommand<Unit, Unit> EnqueueCommand { get; }

		public ReactiveCommand<Unit, Unit> DequeueCommand { get; }

		public ReactiveCommand<Unit, string> PrivacySomeCommand { get; }
		public ReactiveCommand<Unit, string> PrivacyFineCommand { get; }
		public ReactiveCommand<Unit, string> PrivacyStrongCommand { get; }
		public ReactiveCommand<Unit, Unit> TargetButtonCommand { get; }

		public override void OnOpen(CompositeDisposable disposables)
		{
			base.OnOpen(disposables);

			CoinJoinUntilAnonymitySet = Global.Config.MixUntilAnonymitySet;

			var registrableRound = Wallet.ChaumianClient.State.GetRegistrableRoundOrDefault();

			UpdateRequiredBtcLabel(registrableRound);

			CoordinatorFeePercent = registrableRound?.State?.CoordinatorFeePercent.ToString() ?? "0.003";

			Observable.FromEventPattern(Wallet.ChaumianClient, nameof(Wallet.ChaumianClient.CoinQueued))
				.Merge(Observable.FromEventPattern(Wallet.ChaumianClient, nameof(Wallet.ChaumianClient.OnDequeue)))
				.Merge(Observable.FromEventPattern(Wallet.ChaumianClient, nameof(Wallet.ChaumianClient.StateUpdated)))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => UpdateStates())
				.DisposeWith(disposables);

			ClientRound mostAdvancedRound = Wallet.ChaumianClient?.State?.GetMostAdvancedRoundOrDefault();

			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				RoundPhaseState = new RoundPhaseState(mostAdvancedRound.State.Phase, Wallet.ChaumianClient?.State.IsInErrorState ?? false);
				RoundTimesout = mostAdvancedRound.State.Phase == RoundPhase.InputRegistration ? mostAdvancedRound.State.InputRegistrationTimesout : DateTimeOffset.UtcNow;
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
			else
			{
				RoundId = -1;
				RoundPhaseState = new RoundPhaseState(RoundPhase.InputRegistration, false);
				RoundTimesout = DateTimeOffset.UtcNow;
				PeersRegistered = 0;
				PeersNeeded = 100;
			}

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(AmountQueued));
				this.RaisePropertyChanged(nameof(IsLurkingWifeMode));
			}).DisposeWith(disposables);

			Observable.Interval(TimeSpan.FromSeconds(1))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					TimeSpan left = RoundTimesout - DateTimeOffset.UtcNow;
					TimeLeftTillRoundTimeout = left > TimeSpan.Zero ? left : TimeSpan.Zero; // Make sure cannot be less than zero.
				}).DisposeWith(disposables);
		}

		public override bool OnClose()
		{
			CoinsList.OnClose();

			return base.OnClose();
		}

		private async Task DoDequeueAsync(params SmartCoin[] coins)
			=> await DoDequeueAsync(coins as IEnumerable<SmartCoin>);

		private async Task DoDequeueAsync(IEnumerable<SmartCoin> coins)
		{
			IsDequeueBusy = true;
			try
			{
				if (!coins.Any())
				{
					NotificationHelpers.Warning("No coins are selected.", "");
					return;
				}

				try
				{
					await Wallet.ChaumianClient.DequeueCoinsFromMixAsync(coins.ToArray(), DequeueReason.UserRequested);
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex);
				}

				Password = "";
			}
			finally
			{
				IsDequeueBusy = false;
			}
		}

		private async Task DoEnqueueAsync(IEnumerable<SmartCoin> coins)
		{
			IsEnqueueBusy = true;
			try
			{
				if (!coins.Any())
				{
					NotificationHelpers.Warning("No coins are selected.", "");
					return;
				}
				try
				{
					PasswordHelper.GetMasterExtKey(Wallet.KeyManager, Password, out string compatiblityPassword); // If the password is not correct we throw.

					if (compatiblityPassword != null)
					{
						Password = compatiblityPassword;
						NotificationHelpers.Warning(PasswordHelper.CompatibilityPasswordWarnMessage);
					}

					await Wallet.ChaumianClient.QueueCoinsToMixAsync(Password, coins.ToArray());
				}
				catch (SecurityException ex)
				{
					NotificationHelpers.Error(ex.Message, "");
				}
				catch (Exception ex)
				{
					var builder = new StringBuilder(ex.ToTypeMessageString());
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							builder.Append(Environment.NewLine + iex.ToTypeMessageString());
						}
					}
					NotificationHelpers.Error(builder.ToString());
					Logger.LogError(ex);
				}

				Password = "";
			}
			finally
			{
				IsEnqueueBusy = false;
			}
		}

		private void UpdateStates()
		{
			var chaumianClient = Wallet?.ChaumianClient;
			if (chaumianClient is null)
			{
				return;
			}

			AmountQueued = chaumianClient.State.SumAllQueuedCoinAmounts();
			MainWindowViewModel.Instance.CanClose = AmountQueued == Money.Zero;

			var registrableRound = chaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				UpdateRequiredBtcLabel(registrableRound);
			}
			var mostAdvancedRound = chaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				if (!chaumianClient.State.IsInErrorState)
				{
					RoundPhaseState = new RoundPhaseState(mostAdvancedRound.State.Phase, false);
					RoundTimesout = mostAdvancedRound.State.Phase == RoundPhase.InputRegistration ? mostAdvancedRound.State.InputRegistrationTimesout : DateTimeOffset.UtcNow;
				}
				else
				{
					RoundPhaseState = new RoundPhaseState(RoundPhaseState.Phase, true);
				}

				this.RaisePropertyChanged(nameof(RoundPhaseState));
				this.RaisePropertyChanged(nameof(RoundTimesout));
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
		}

		private void UpdateRequiredBtcLabel(ClientRound registrableRound)
		{
			if (Wallet is null)
			{
				return; // Otherwise NullReferenceException at shutdown.
			}

			if (registrableRound == default)
			{
				if (RequiredBTC == default)
				{
					RequiredBTC = Money.Zero;
				}
			}
			else
			{
				var coins = Wallet.Coins;
				var queued = coins.CoinJoinInProcess();
				if (queued.Any())
				{
					RequiredBTC = registrableRound.State.CalculateRequiredAmount(Wallet.ChaumianClient.State.GetAllQueuedCoinAmounts().ToArray());
				}
				else
				{
					var available = coins.Confirmed().Available();
					RequiredBTC = available.Any()
						? registrableRound.State.CalculateRequiredAmount(available.Where(x => x.AnonymitySet < Global.Config.PrivacyLevelStrong).Select(x => x.Amount).ToArray())
						: registrableRound.State.CalculateRequiredAmount();
				}
			}
		}

		public override void OnSelected()
		{
			base.OnSelected();
			Wallet.ChaumianClient.ActivateFrequentStatusProcessing();
		}

		public override void OnDeselected()
		{
			Wallet.ChaumianClient.DeactivateFrequentStatusProcessingIfNotMixing();
			base.OnDeselected();
		}

		private void ValidatePassword(IValidationErrors errors) => PasswordHelper.ValidatePassword(errors, Password);
	}
}
