using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using WalletWasabi.Models;
using NBitcoin;
using System.Reactive.Linq;
using System.Linq;
using WalletWasabi.Gui.Models;
using WalletWasabi.Models.ChaumianCoinJoin;
using System.Globalization;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinViewModel : ViewModelBase
	{
		private bool _isSelected;
		private SmartCoinStatus _smartCoinStatus;

		public CoinViewModel(SmartCoin model)
		{
			Model = model;

			model.WhenAnyValue(x => x.Confirmed).ObserveOn(RxApp.MainThreadScheduler).Subscribe(confirmed =>
			{
				this.RaisePropertyChanged(nameof(Confirmed));
				RefreshSmartCoinStatus();
			});

			model.WhenAnyValue(x => x.SpentOrCoinJoinInProgress).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(SpentOrCoinJoinInProgress));
			});

			model.WhenAnyValue(x => x.CoinJoinInProgress).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(CoinJoinInProgress));
				RefreshSmartCoinStatus();
			});

			model.WhenAnyValue(x => x.IsBanned).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				RefreshSmartCoinStatus();
			});

			model.WhenAnyValue(x => x.SpentAccordingToBackend).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				RefreshSmartCoinStatus();
			});

			this.WhenAnyValue(x => x.Status).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(ToolTip));
			});

			Global.IndexDownloader.WhenAnyValue(x => x.BestHeight).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(Confirmations));
				RefreshSmartCoinStatus();
			});

			Global.ChaumianClient.StateUpdated += ChaumianClient_StateUpdated;
		}

		private void ChaumianClient_StateUpdated(object sender, EventArgs e)
		{
			this.RaisePropertyChanged(nameof(Status));
		}

		public SmartCoin Model { get; }

		public bool Confirmed => Model.Confirmed;

		public bool CoinJoinInProgress => Model.CoinJoinInProgress;

		public bool SpentOrCoinJoinInProgress => Model.SpentOrCoinJoinInProgress;

		public bool Unspent => Model.Unspent;

		public string Address => Model.ScriptPubKey.GetDestinationAddress(Global.Network).ToString();

		public int Confirmations => Model.Height.Type == HeightType.Chain
			? Global.IndexDownloader.BestHeight.Value - Model.Height.Value + 1
			: 0;

		public bool IsSelected
		{
			get { return _isSelected; }
			set { this.RaiseAndSetIfChanged(ref _isSelected, value); }
		}

		public string ToolTip
		{
			get
			{
				switch (Status)
				{
					case SmartCoinStatus.Confirmed: return "This coin is confirmed.";
					case SmartCoinStatus.Unconfirmed: return "This coin is unconfirmed.";
					case SmartCoinStatus.MixingOnWaitingList: return "This coin is waiting for its turn to be coinjoined.";
					case SmartCoinStatus.MixingBanned: return $"The coordinator banned this coin from participation until {Model.BannedUntilUtc.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}.";
					case SmartCoinStatus.MixingInputRegistration: return "This coin is registered for coinjoin.";
					case SmartCoinStatus.MixingConnectionConfirmation: return "This coin is currently in Connection Confirmation phase.";
					case SmartCoinStatus.MixingOutputRegistration: return "This coin is currently in Output Registration phase.";
					case SmartCoinStatus.MixingSigning: return "This coin is currently in Signing phase.";
					case SmartCoinStatus.SpentAccordingToBackend: return "According to the Backend, this coin is spent. Wallet state will be corrected after confirmation.";
					case SmartCoinStatus.MixingWaitingForConfirmation: return "Coinjoining unconfirmed coins is not allowed, unless the coin is a coinjoin output itself.";
					default: return "This is impossible.";
				}
			}
		}

		public Money Amount => Model.Amount;

		public string AmountBtc => Model.Amount.ToString(false, true);

		public string Label => Model.Label;

		public int Height => Model.Height;

		public string TransactionId => Model.TransactionId.ToString();

		public uint OutputIndex => Model.Index;

		public int AnonymitySet => Model.AnonymitySet;

		public string InCoinJoin => Model.CoinJoinInProgress ? "Yes" : "No";

		public string History => string.Join(", ", Global.WalletService.GetHistory(Model, Enumerable.Empty<SmartCoin>()).Select(x => x.Label).Distinct());



		public SmartCoinStatus Status
		{
			get
			{
				return _smartCoinStatus;
			}
			set
			{
				this.RaiseAndSetIfChanged(ref _smartCoinStatus, value);
			}
		}

		void RefreshSmartCoinStatus()
		{
			Status = GetSmartCoinStatus(); 
		}

		SmartCoinStatus GetSmartCoinStatus()
		{
			if (Model.IsBanned)
			{
				return SmartCoinStatus.MixingBanned;
			}

			CcjClientState clientState = Global.ChaumianClient.State;

			if (Model.CoinJoinInProgress)
			{
				foreach (long roundId in clientState.GetAllMixingRounds())
				{
					CcjClientRound round = clientState.GetSingleOrDefaultRound(roundId);
					if (round != default)
					{
						if (round.CoinsRegistered.Contains(Model))
						{
							if (round.State.Phase == CcjRoundPhase.InputRegistration)
							{
								return SmartCoinStatus.MixingInputRegistration;
							}
							else if (round.State.Phase == CcjRoundPhase.ConnectionConfirmation)
							{
								return SmartCoinStatus.MixingConnectionConfirmation;
							}
							else if (round.State.Phase == CcjRoundPhase.OutputRegistration)
							{
								return SmartCoinStatus.MixingOutputRegistration;
							}
							else if (round.State.Phase == CcjRoundPhase.Signing)
							{
								return SmartCoinStatus.MixingSigning;
							}
						}
					}
				}
			}

			if (Model.SpentAccordingToBackend)
			{
				return SmartCoinStatus.SpentAccordingToBackend;
			}

			if (Model.Confirmed)
			{
				if (Model.CoinJoinInProgress)
				{
					return SmartCoinStatus.MixingOnWaitingList;
				}
				else
				{
					return SmartCoinStatus.Confirmed;
				}
			}
			else // Unconfirmed
			{
				if (Model.CoinJoinInProgress)
				{
					return SmartCoinStatus.MixingWaitingForConfirmation;
				}
				else
				{
					return SmartCoinStatus.Unconfirmed;
				}
			}
		}

	}
}
