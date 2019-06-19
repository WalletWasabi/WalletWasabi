using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.KeyManagement;
using WalletWasabi.Logging;
using WalletWasabi.Models.ChaumianCoinJoin;
using WalletWasabi.Services;
using static WalletWasabi.Gui.Config;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		private CompositeDisposable Disposables { get; set; }

		private long _roundId;
		private int _successfulRoundCount;
		private CcjRoundPhase _phase;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		private int _peersRegistered;
		private int _peersNeeded;
		private string _password;
		private Money _amountQueued;
		private bool _isEnqueueBusy;
		private bool _isDequeueBusy;
		private string _enqueueButtonText;
		private const string EnqueueButtonTextString = "Enqueue Selected Coins";
		private const string EnqueuingButtonTextString = "Queuing coins...";
		private string _dequeueButtonText;
		private const string DequeueButtonTextString = "Dequeue Selected Coins";
		private const string DequeuingButtonTextString = "Dequeuing coins...";
		private int _coinJoinUntilAnonimitySet;
		private TargetPrivacy _targetPrivacy;

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			Password = "";

			CoinsList = new CoinListViewModel();

			Observable.FromEventPattern(CoinsList, nameof(CoinsList.DequeueCoinsPressed)).Subscribe(_ => OnCoinsListDequeueCoinsPressedAsync());

			AmountQueued = Money.Zero; // Global.Instance.ChaumianClient.State.SumAllQueuedCoinAmounts();

			EnqueueCommand = ReactiveCommand.CreateFromTask(async () => await DoEnqueueAsync(CoinsList.Coins.Where(c => c.IsSelected)));

			DequeueCommand = ReactiveCommand.CreateFromTask(async () => await DoDequeueAsync(CoinsList.Coins.Where(c => c.IsSelected)));

			PrivacySomeCommand = ReactiveCommand.Create(() => TargetPrivacy = TargetPrivacy.Some);

			PrivacyFineCommand = ReactiveCommand.Create(() => TargetPrivacy = TargetPrivacy.Fine);

			PrivacyStrongCommand = ReactiveCommand.Create(() => TargetPrivacy = TargetPrivacy.Strong);

			TargetButtonCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				switch (TargetPrivacy)
				{
					case TargetPrivacy.None:
						TargetPrivacy = TargetPrivacy.Some;
						break;

					case TargetPrivacy.Some:
						TargetPrivacy = TargetPrivacy.Fine;
						break;

					case TargetPrivacy.Fine:
						TargetPrivacy = TargetPrivacy.Strong;
						break;

					case TargetPrivacy.Strong:
						TargetPrivacy = TargetPrivacy.Some;
						break;
				}
				Global.Instance.Config.MixUntilAnonymitySet = CoinJoinUntilAnonimitySet;
				await Global.Instance.Config.ToFileAsync();
			});

			this.WhenAnyValue(x => x.Password).Subscribe(async x =>
			{
				try
				{
					if (x.NotNullAndNotEmpty())
					{
						char lastChar = x.Last();
						if (lastChar == '\r' || lastChar == '\n') // If the last character is cr or lf then act like it'd be a sign to do the job.
						{
							Password = x.TrimEnd('\r', '\n');
							await DoEnqueueAsync(CoinsList.Coins.Where(c => c.IsSelected));
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogTrace(ex);
				}
			});

			this.WhenAnyValue(x => x.IsEnqueueBusy)
				.Select(x => x ? EnqueuingButtonTextString : EnqueueButtonTextString)
				.Subscribe(text => EnqueueButtonText = text);

			this.WhenAnyValue(x => x.IsDequeueBusy)
				.Select(x => x ? DequeuingButtonTextString : DequeueButtonTextString)
				.Subscribe(text => DequeueButtonText = text);

			this.WhenAnyValue(x => x.TargetPrivacy).Subscribe(target =>
			{
				CoinJoinUntilAnonimitySet = Global.Instance.Config.GetTargetLevel(target);
			});
		}

		public override void OnOpen()
		{
			CoinsList.OnOpen();

			if (Disposables != null)
			{
				throw new Exception("CoinJoin tab opened before previous closed.");
			}

			Disposables = new CompositeDisposable();

			TargetPrivacy = Global.Instance.Config.GetTargetPrivacy();

			var registrableRound = Global.Instance.ChaumianClient.State.GetRegistrableRoundOrDefault();

			UpdateRequiredBtcLabel(registrableRound);

			CoordinatorFeePercent = registrableRound?.State?.CoordinatorFeePercent.ToString() ?? "0.003";

			Observable.FromEventPattern(Global.Instance.ChaumianClient, nameof(Global.Instance.ChaumianClient.CoinQueued))
				.Merge(Observable.FromEventPattern(Global.Instance.ChaumianClient, nameof(Global.Instance.ChaumianClient.CoinDequeued)))
				.Merge(Observable.FromEventPattern(Global.Instance.ChaumianClient, nameof(Global.Instance.ChaumianClient.StateUpdated)))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => UpdateStates())
				.DisposeWith(Disposables);

			CcjClientRound mostAdvancedRound = Global.Instance.ChaumianClient?.State?.GetMostAdvancedRoundOrDefault();

			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				SuccessfulRoundCount = mostAdvancedRound.State.SuccessfulRoundCount;
				Phase = mostAdvancedRound.State.Phase;
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
			else
			{
				RoundId = -1;
				SuccessfulRoundCount = -1;
				Phase = CcjRoundPhase.InputRegistration;
				PeersRegistered = 0;
				PeersNeeded = 100;
			}

			Global.Instance.UiConfig.WhenAnyValue(x => x.LurkingWifeMode).ObserveOn(RxApp.MainThreadScheduler).Subscribe(x =>
			{
				this.RaisePropertyChanged(nameof(AmountQueued));
				this.RaisePropertyChanged(nameof(IsLurkingWifeMode));
			}).DisposeWith(Disposables);

			base.OnOpen();
		}

		public override bool OnClose()
		{
			CoinsList.OnClose();

			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}

		private async Task DoDequeueAsync(IEnumerable<CoinViewModel> selectedCoins)
		{
			IsDequeueBusy = true;
			try
			{
				SetWarningMessage("");

				if (!selectedCoins.Any())
				{
					SetWarningMessage("No coins are selected to dequeue.");
					return;
				}

				try
				{
					await Global.Instance.ChaumianClient.DequeueCoinsFromMixAsync(selectedCoins.Select(c => c.Model).ToArray(), "Dequeued by the user.");
				}
				catch (Exception ex)
				{
					Logger.LogWarning<CoinJoinTabViewModel>(ex);
					var builder = new StringBuilder(ex.ToTypeMessageString());
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							builder.Append(Environment.NewLine + iex.ToTypeMessageString());
						}
					}
					SetWarningMessage(builder.ToString());
					return;
				}
			}
			finally
			{
				IsDequeueBusy = false;
			}
		}

		private async Task DoEnqueueAsync(IEnumerable<CoinViewModel> selectedCoins)
		{
			IsEnqueueBusy = true;
			try
			{
				SetWarningMessage("");
				Password = Guard.Correct(Password);

				if (!selectedCoins.Any())
				{
					SetWarningMessage("No coins are selected to enqueue.");
					return;
				}

				try
				{
					await Global.Instance.ChaumianClient.QueueCoinsToMixAsync(Password, selectedCoins.Select(c => c.Model).ToArray());
				}
				catch (Exception ex)
				{
					Logger.LogWarning<CoinJoinTabViewModel>(ex);
					var builder = new StringBuilder(ex.ToTypeMessageString());
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							builder.Append(Environment.NewLine + iex.ToTypeMessageString());
						}
					}
					SetWarningMessage(builder.ToString());
					Password = string.Empty;
					return;
				}

				Password = string.Empty;
			}
			finally
			{
				IsEnqueueBusy = false;
			}
		}

		private void UpdateStates()
		{
			AmountQueued = Global.Instance.ChaumianClient.State.SumAllQueuedCoinAmounts();
			MainWindowViewModel.Instance.CanClose = AmountQueued == Money.Zero;

			var registrableRound = Global.Instance.ChaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				UpdateRequiredBtcLabel(registrableRound);
			}
			var mostAdvancedRound = Global.Instance.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				SuccessfulRoundCount = mostAdvancedRound.State.SuccessfulRoundCount;
				if (!Global.Instance.ChaumianClient.State.IsInErrorState)
				{
					Phase = mostAdvancedRound.State.Phase;
				}
				this.RaisePropertyChanged(nameof(Phase));
				PeersRegistered = mostAdvancedRound.State.RegisteredPeerCount;
				PeersNeeded = mostAdvancedRound.State.RequiredPeerCount;
			}
		}

#pragma warning disable CS0618 // Type or member is obsolete

		private void UpdateRequiredBtcLabel(CcjClientRound registrableRound)
#pragma warning restore CS0618 // Type or member is obsolete
		{
			if (Global.Instance.WalletService is null)
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
				var queued = Global.Instance.WalletService.Coins.Where(x => x.CoinJoinInProgress);
				if (queued.Any())
				{
					RequiredBTC = registrableRound.State.CalculateRequiredAmount(Global.Instance.ChaumianClient.State.GetAllQueuedCoinAmounts().ToArray());
				}
				else
				{
					var available = Global.Instance.WalletService.Coins.Where(x => x.Confirmed && !x.Unavailable);
					if (available.Any())
					{
						RequiredBTC = registrableRound.State.CalculateRequiredAmount(available.Where(x => x.AnonymitySet < Global.Instance.Config.PrivacyLevelStrong).Select(x => x.Amount).ToArray());
					}
					else
					{
						RequiredBTC = registrableRound.State.CalculateRequiredAmount();
					}
				}
			}
		}

		public override void OnSelected()
		{
			Global.Instance.ChaumianClient.ActivateFrequentStatusProcessing();
		}

		public override void OnDeselected()
		{
			Global.Instance.ChaumianClient.DeactivateFrequentStatusProcessingIfNotMixing();
		}

		public string Password
		{
			get => _password;
			set => this.RaiseAndSetIfChanged(ref _password, value);
		}

		public CoinListViewModel CoinsList { get; }

		private async void OnCoinsListDequeueCoinsPressedAsync()
		{
			try
			{
				var selectedCoin = CoinsList.SelectedCoin;
				if (selectedCoin is null)
				{
					return;
				}

				await DoDequeueAsync(new[] { selectedCoin });
			}
			catch (Exception ex)
			{
				Logger.LogWarning<CoinJoinTabViewModel>(ex);
			}
		}

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

		public int SuccessfulRoundCount
		{
			get => _successfulRoundCount;
			set => this.RaiseAndSetIfChanged(ref _successfulRoundCount, value);
		}

		public CcjRoundPhase Phase
		{
			get => _phase;
			set => this.RaiseAndSetIfChanged(ref _phase, value);
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

		public int CoinJoinUntilAnonimitySet
		{
			get => _coinJoinUntilAnonimitySet;
			set => this.RaiseAndSetIfChanged(ref _coinJoinUntilAnonimitySet, value);
		}

		private TargetPrivacy TargetPrivacy
		{
			get => _targetPrivacy;
			set => this.RaiseAndSetIfChanged(ref _targetPrivacy, value);
		}

		public bool IsLurkingWifeMode => Global.Instance.UiConfig.LurkingWifeMode is true;

		public ReactiveCommand<Unit, Unit> EnqueueCommand { get; }

		public ReactiveCommand<Unit, Unit> DequeueCommand { get; }

		public ReactiveCommand<Unit, TargetPrivacy> PrivacySomeCommand { get; }
		public ReactiveCommand<Unit, TargetPrivacy> PrivacyFineCommand { get; }
		public ReactiveCommand<Unit, TargetPrivacy> PrivacyStrongCommand { get; }
		public ReactiveCommand<Unit, Unit> TargetButtonCommand { get; }
	}
}
