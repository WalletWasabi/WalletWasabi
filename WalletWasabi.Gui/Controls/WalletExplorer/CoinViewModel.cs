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
using System.Threading.Tasks;
using System.Reactive.Disposables;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinViewModel : ViewModelBase, IDisposable
	{
		private CompositeDisposable Disposables { get; }

		private bool _isSelected;
		private SmartCoinStatus _smartCoinStatus;

		public CoinViewModel(SmartCoin model)
		{
			Disposables = new CompositeDisposable();

			Model = model;

			model.WhenAnyValue(x => x.Confirmed).ObserveOn(RxApp.MainThreadScheduler).Subscribe(confirmed =>
			{
				RefreshSmartCoinStatus();
				this.RaisePropertyChanged(nameof(Confirmed));
			}).DisposeWith(Disposables);

			model.WhenAnyValue(x => x.Unavailable).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(Unavailable));
			}).DisposeWith(Disposables);

			model.WhenAnyValue(x => x.CoinJoinInProgress).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				RefreshSmartCoinStatus();
				this.RaisePropertyChanged(nameof(CoinJoinInProgress));
			}).DisposeWith(Disposables);

			model.WhenAnyValue(x => x.IsBanned).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				RefreshSmartCoinStatus();
			}).DisposeWith(Disposables);

			model.WhenAnyValue(x => x.SpentAccordingToBackend).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				RefreshSmartCoinStatus();
			}).DisposeWith(Disposables);

			model.WhenAnyValue(x => x.Unspent).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(Unspent));
			}).DisposeWith(Disposables);

			model.WhenAnyValue(x => x.Clusters).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(Clusters));
			}).DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Status).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(ToolTip));
			}).DisposeWith(Disposables);

			Global.Synchronizer.WhenAnyValue(x => x.BestBlockchainHeight).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				RefreshSmartCoinStatus();
				this.RaisePropertyChanged(nameof(Confirmations));
			}).DisposeWith(Disposables);

			Global.ChaumianClient.StateUpdated += ChaumianClient_StateUpdated;
		}

		private void ChaumianClient_StateUpdated(object sender, EventArgs e)
		{
			RefreshSmartCoinStatus();
		}

		public SmartCoin Model { get; }

		public bool Confirmed => Model.Confirmed;

		public bool CoinJoinInProgress => Model.CoinJoinInProgress;

		public bool Unavailable => Model.Unavailable;

		public bool Unspent => Model.Unspent;

		public string Address => Model.ScriptPubKey.GetDestinationAddress(Global.Network).ToString();

		public int Confirmations => Model.Height.Type == HeightType.Chain
			? Global.Synchronizer.BestBlockchainHeight.Value - Model.Height.Value + 1
			: 0;

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
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
					case SmartCoinStatus.MixingBanned: return $"The coordinator banned this coin from participation until {Model?.BannedUntilUtc?.ToString("yyyy - MM - dd HH: mm", CultureInfo.InvariantCulture)}.";
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

		public string Clusters => Model.Clusters;

		public string PubKey => Model.HdPubKey.PubKey.ToString();

		public string KeyPath => Model.HdPubKey.FullKeyPath.ToString();

		public SmartCoinStatus Status
		{
			get => _smartCoinStatus;
			set => this.RaiseAndSetIfChanged(ref _smartCoinStatus, value);
		}

		private void RefreshSmartCoinStatus()
		{
			Status = GetSmartCoinStatus();
		}

		private SmartCoinStatus GetSmartCoinStatus()
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

		#region IDisposable Support

		private volatile bool _disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					if (Global.ChaumianClient != null)
					{
						Global.ChaumianClient.StateUpdated -= ChaumianClient_StateUpdated;
					}

					Disposables?.Dispose();
				}

				_disposedValue = true;
			}
		}

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
		}

		#endregion IDisposable Support
	}
}
