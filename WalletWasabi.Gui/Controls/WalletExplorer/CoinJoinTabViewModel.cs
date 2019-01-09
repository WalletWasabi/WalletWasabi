using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using AvalonStudio.Commands;
using NBitcoin;
using ReactiveUI;
using ReactiveUI.Legacy;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
using static WalletWasabi.Gui.Models.ShieldLevelHelper;
using static WalletWasabi.Models.ServiceConfiguration;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinJoinTabViewModel : WalletActionViewModel
	{
		private CoinListViewModel _coinsList;
		private long _roundId;
		private int _successfulRoundCount;
		private CcjRoundPhase _phase;
		private Money _requiredBTC;
		private string _coordinatorFeePercent;
		private int _peersRegistered;
		private int _peersNeeded;
		private string _password;
		private Money _amountQueued;
		private string _warningMessage;
		private bool _isEnqueueBusy;
		private bool _isDequeueBusy;
		private string _enqueueButtonText;
		private const string EnqueueButtonTextString = "Enqueue Selected Coins";
		private const string EnqueuingButtonTextString = "Queuing coins...";
		private string _dequeueButtonText;
		private const string DequeueButtonTextString = "Dequeue Selected Coins";
		private const string DequeuingButtonTextString = "Dequeuing coins...";

		public CoinJoinTabViewModel(WalletViewModel walletViewModel)
			: base("CoinJoin", walletViewModel)
		{
			Password = "";
			TargetPrivacy = ShieldLevelHelper.GetTargetPrivacy(Global.Config.MixUntilAnonymitySet);

			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();

			UpdateRequiredBtcLabel(registrableRound);

			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
			}
			else
			{
				CoordinatorFeePercent = "0.003";
			}

			if (!(registrableRound?.State?.Denomination is null) && registrableRound.State.Denomination != Money.Zero)
			{
				CoinsList = new CoinListViewModel();
			}
			else
			{
				CoinsList = new CoinListViewModel();
			}

			//CoinsList = new CoinListViewModel(coins);

			AmountQueued = Money.Zero;// Global.ChaumianClient.State.SumAllQueuedCoinAmounts();

			Global.ChaumianClient.CoinQueued += ChaumianClient_CoinQueued;
			Global.ChaumianClient.CoinDequeued += ChaumianClient_CoinDequeued;

			var mostAdvancedRound = Global.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
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
			Global.ChaumianClient.StateUpdated += ChaumianClient_StateUpdated;

			EnqueueCommand = ReactiveCommand.Create(async () =>
			{
				await DoEnqueueAsync(CoinsList.Coins.Where(c => c.IsSelected));
			});

			DequeueCommand = ReactiveCommand.Create(async () =>
			{
				await DoDequeueAsync(CoinsList.Coins.Where(c => c.IsSelected));
			});

			PrivacySomeCommand = ReactiveCommand.Create(() =>
			{
				TargetPrivacy = TargetPrivacy.Some;
			});
			PrivacyFineCommand = ReactiveCommand.Create(() =>
			{
				TargetPrivacy = TargetPrivacy.Fine;
			});
			PrivacyStrongCommand = ReactiveCommand.Create(() =>
			{
				TargetPrivacy = TargetPrivacy.Strong;
			});
			TargetButtonCommand = ReactiveCommand.Create(() =>
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
			});

			this.WhenAnyValue(x => x.Password).Subscribe(async x =>
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
			});

			this.WhenAnyValue(x => x.IsEnqueueBusy).Subscribe(busy =>
			{
				if (busy)
				{
					EnqueueButtonText = EnqueuingButtonTextString;
				}
				else
				{
					EnqueueButtonText = EnqueueButtonTextString;
				}
			});

			this.WhenAnyValue(x => x.IsDequeueBusy).Subscribe(busy =>
			{
				if (busy)
				{
					DequeueButtonText = DequeuingButtonTextString;
				}
				else
				{
					DequeueButtonText = DequeueButtonTextString;
				}
			});

			this.WhenAnyValue(x => x.CoinJoinUntilAnonimitySet).Subscribe(async coinJoinUntilAnonimitySet =>
			{
				Global.Config.MixUntilAnonymitySet = coinJoinUntilAnonimitySet;
				await Global.Config.ToFileAsync();
			});

			this.WhenAnyValue(x => x.TargetPrivacy).Subscribe(target =>
			{
				CoinJoinUntilAnonimitySet = GetTargetLevel(target);
			});
		}

		private async Task DoDequeueAsync(IEnumerable<CoinViewModel> selectedCoins)
		{
			IsDequeueBusy = true;
			try
			{
				WarningMessage = "";

				if (!selectedCoins.Any())
				{
					SetWarningMessage("No coins are selected to dequeue.");
					return;
				}

				try
				{
					await Global.ChaumianClient.DequeueCoinsFromMixAsync(selectedCoins.Select(c => c.Model).ToArray());
				}
				catch (Exception ex)
				{
					Logger.LogWarning<CoinJoinTabViewModel>(ex);
					var warningMessage = ex.ToTypeMessageString();
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							warningMessage += Environment.NewLine + iex.ToTypeMessageString();
						}
					}
					SetWarningMessage(warningMessage);
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
				WarningMessage = "";
				Password = Guard.Correct(Password);

				if (!selectedCoins.Any())
				{
					SetWarningMessage("No coins are selected to enqueue.");
					return;
				}

				try
				{
					await Global.ChaumianClient.QueueCoinsToMixAsync(Password, selectedCoins.Select(c => c.Model).ToArray());
				}
				catch (Exception ex)
				{
					Logger.LogWarning<CoinJoinTabViewModel>(ex);
					var warningMessage = ex.ToTypeMessageString();
					if (ex is AggregateException aggex)
					{
						foreach (var iex in aggex.InnerExceptions)
						{
							warningMessage += Environment.NewLine + iex.ToTypeMessageString();
						}
					}
					SetWarningMessage(warningMessage);
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

		private void ChaumianClient_CoinDequeued(object sender, SmartCoin e)
		{
			UpdateStates();
		}

		private void ChaumianClient_CoinQueued(object sender, SmartCoin e)
		{
			UpdateStates();
		}

		private void ChaumianClient_StateUpdated(object sender, EventArgs e)
		{
			UpdateStates();
		}

		private void UpdateStates()
		{
			AmountQueued = Global.ChaumianClient.State.SumAllQueuedCoinAmounts();
			MainWindowViewModel.Instance.CanClose = AmountQueued == Money.Zero;

			var registrableRound = Global.ChaumianClient.State.GetRegistrableRoundOrDefault();
			if (registrableRound != default)
			{
				CoordinatorFeePercent = registrableRound.State.CoordinatorFeePercent.ToString();
				UpdateRequiredBtcLabel(registrableRound);
			}
			var mostAdvancedRound = Global.ChaumianClient.State.GetMostAdvancedRoundOrDefault();
			if (mostAdvancedRound != default)
			{
				RoundId = mostAdvancedRound.State.RoundId;
				SuccessfulRoundCount = mostAdvancedRound.State.SuccessfulRoundCount;
				if (!Global.ChaumianClient.State.IsInErrorState)
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
			if (Global.WalletService is null)
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
				var queued = Global.WalletService.Coins.Where(x => x.CoinJoinInProgress);
				if (queued.Any())
				{
					RequiredBTC = registrableRound.State.CalculateRequiredAmount(Global.ChaumianClient.State.GetAllQueuedCoinAmounts().ToArray());
				}
				else
				{
					var available = Global.WalletService.Coins.Where(x => x.Confirmed && !x.SpentOrCoinJoinInProgress);
					if (available.Any())
					{
						RequiredBTC = registrableRound.State.CalculateRequiredAmount(available.Where(x => x.AnonymitySet < Global.Config.PrivacyLevelStrong).Select(x => x.Amount).ToArray());
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
			Global.ChaumianClient.ActivateFrequentStatusProcessing();
		}

		public override void OnDeselected()
		{
			Global.ChaumianClient.DeactivateFrequentStatusProcessingIfNotMixing();
		}

		private void SetWarningMessage(string message)
		{
			WarningMessage = message;

			Dispatcher.UIThread.Post(async () =>
			{
				await Task.Delay(7000);
				if (WarningMessage == message)
				{
					WarningMessage = "";
				}
			});
		}

		public string Password
		{
			get { return _password; }
			set { this.RaiseAndSetIfChanged(ref _password, value); }
		}

		public CoinListViewModel CoinsList
		{
			get { return _coinsList; }
			set
			{
				bool changed = _coinsList != value;
				if (_coinsList != null) _coinsList.DequeueCoinsPressed -= CoinsList_DequeueCoinsPressedAsync;
				this.RaiseAndSetIfChanged(ref _coinsList, value);
				_coinsList.DequeueCoinsPressed += CoinsList_DequeueCoinsPressedAsync;
			}
		}

#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void

		private async void CoinsList_DequeueCoinsPressedAsync()
#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
		{
			var selectedCoin = _coinsList?.SelectedCoin;
			if (selectedCoin == null) return;
			await DoDequeueAsync(new[] { selectedCoin });
		}

		public Money AmountQueued
		{
			get { return _amountQueued; }
			set { this.RaiseAndSetIfChanged(ref _amountQueued, value); }
		}

		public long RoundId
		{
			get { return _roundId; }
			set { this.RaiseAndSetIfChanged(ref _roundId, value); }
		}

		public int SuccessfulRoundCount
		{
			get { return _successfulRoundCount; }
			set { this.RaiseAndSetIfChanged(ref _successfulRoundCount, value); }
		}

		public CcjRoundPhase Phase
		{
			get { return _phase; }
			set
			{
				this.RaiseAndSetIfChanged(ref _phase, value);
			}
		}

		public Money RequiredBTC
		{
			get { return _requiredBTC; }
			set { this.RaiseAndSetIfChanged(ref _requiredBTC, value); }
		}

		public string CoordinatorFeePercent
		{
			get { return _coordinatorFeePercent; }
			set { this.RaiseAndSetIfChanged(ref _coordinatorFeePercent, value); }
		}

		public int PeersRegistered
		{
			get { return _peersRegistered; }
			set { this.RaiseAndSetIfChanged(ref _peersRegistered, value); }
		}

		public int PeersNeeded
		{
			get { return _peersNeeded; }
			set { this.RaiseAndSetIfChanged(ref _peersNeeded, value); }
		}

		public string WarningMessage
		{
			get { return _warningMessage; }
			set { this.RaiseAndSetIfChanged(ref _warningMessage, value); }
		}

		public bool IsEnqueueBusy
		{
			get { return _isEnqueueBusy; }
			set { this.RaiseAndSetIfChanged(ref _isEnqueueBusy, value); }
		}

		public bool IsDequeueBusy
		{
			get { return _isDequeueBusy; }
			set { this.RaiseAndSetIfChanged(ref _isDequeueBusy, value); }
		}

		public string EnqueueButtonText
		{
			get { return _enqueueButtonText; }
			set { this.RaiseAndSetIfChanged(ref _enqueueButtonText, value); }
		}

		public string DequeueButtonText
		{
			get { return _dequeueButtonText; }
			set { this.RaiseAndSetIfChanged(ref _dequeueButtonText, value); }
		}

		private int _coinJoinUntilAnonimitySet;
		private TargetPrivacy _targetPrivacy;

		public int CoinJoinUntilAnonimitySet
		{
			get { return _coinJoinUntilAnonimitySet; }
			set
			{
				this.RaiseAndSetIfChanged(ref _coinJoinUntilAnonimitySet, value);
			}
		}

		private TargetPrivacy TargetPrivacy
		{
			get => _targetPrivacy;

			set
			{
				this.RaiseAndSetIfChanged(ref _targetPrivacy, value);
			}
		}

		public ReactiveCommand EnqueueCommand { get; }

		public ReactiveCommand DequeueCommand { get; }

		public ReactiveCommand PrivacySomeCommand { get; }
		public ReactiveCommand PrivacyFineCommand { get; }
		public ReactiveCommand PrivacyStrongCommand { get; }
		public ReactiveCommand TargetButtonCommand { get; }
	}
}
