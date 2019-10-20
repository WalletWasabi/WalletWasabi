using NBitcoin;
using ReactiveUI;
using System;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.CoinJoin.Client;
using WalletWasabi.CoinJoin.Common;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class CoinViewModel : ViewModelBase
	{
		public CompositeDisposable Disposables { get; set; }

		private bool _isSelected;
		private SmartCoinStatus _status;
		private ObservableAsPropertyHelper<bool> _coinJoinInProgress;
		private ObservableAsPropertyHelper<bool> _unspent;
		private ObservableAsPropertyHelper<bool> _confirmed;
		private ObservableAsPropertyHelper<bool> _unavailable;
		private CoinListViewModel _owner;
		public Global Global => _owner.Global;

		public CoinViewModel(CoinListViewModel owner, SmartCoin model)
		{
			Model = model;
			_owner = owner;
		}

		public void SubscribeEvents()
		{
			if (Disposables != null)
			{
				throw new Exception("Please report to Dan");
			}

			Disposables = new CompositeDisposable();

			//TODO defer subscription to when accessed (will be faster in ui.)
			_coinJoinInProgress = Model.WhenAnyValue(x => x.CoinJoinInProgress)
				.ToProperty(this, x => x.CoinJoinInProgress, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_unspent = Model.WhenAnyValue(x => x.Unspent)
				.ToProperty(this, x => x.Unspent, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_confirmed = Model.WhenAnyValue(x => x.Confirmed)
				.ToProperty(this, x => x.Confirmed, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			_unavailable = Model.WhenAnyValue(x => x.Unavailable)
				.ToProperty(this, x => x.Unavailable, scheduler: RxApp.MainThreadScheduler)
				.DisposeWith(Disposables);

			this.WhenAnyValue(x => x.Status)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(ToolTip)));

			this.WhenAnyValue(x => x.Confirmed, x => x.CoinJoinInProgress, x => x.Confirmations)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => RefreshSmartCoinStatus());

			this.WhenAnyValue(x => x.IsSelected)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => _owner.OnCoinIsSelectedChanged(this));

			this.WhenAnyValue(x => x.Status)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => _owner.OnCoinStatusChanged());

			this.WhenAnyValue(x => x.Unspent)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => _owner.OnCoinUnspentChanged(this));

			Model.WhenAnyValue(x => x.IsBanned, x => x.SpentAccordingToBackend)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => RefreshSmartCoinStatus())
				.DisposeWith(Disposables);

			Observable.FromEventPattern(
				Global.ChaumianClient,
				nameof(Global.ChaumianClient.StateUpdated))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => RefreshSmartCoinStatus())
				.DisposeWith(Disposables);

			Global.BitcoinStore.HashChain.WhenAnyValue(x => x.ServerTipHeight)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ => this.RaisePropertyChanged(nameof(Confirmations)))
				.DisposeWith(Disposables);

			Global.UiConfig.WhenAnyValue(x => x.LurkingWifeMode)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(_ =>
				{
					this.RaisePropertyChanged(nameof(AmountBtc));
					this.RaisePropertyChanged(nameof(Clusters));
				}).DisposeWith(Disposables);
		}

		public void UnsubscribeEvents()
		{
			Disposables.Dispose();
			Disposables = null;
		}

		public SmartCoin Model { get; }

		public bool Confirmed => _confirmed?.Value ?? false;

		public bool CoinJoinInProgress => _coinJoinInProgress?.Value ?? false;

		public bool Unavailable => _unavailable?.Value ?? false;

		public bool Unspent => _unspent?.Value ?? false;

		public string Address => Model.ScriptPubKey.GetDestinationAddress(Global.Network).ToString();

		public int Confirmations => Model.Height.Type == HeightType.Chain
			? Global.BitcoinStore.HashChain.TipHeight - Model.Height.Value + 1
			: 0;

		public bool IsSelected
		{
			get => _isSelected;
			set => this.RaiseAndSetIfChanged(ref _isSelected, value);
		}

		public string ToolTip => Status switch
		{
			SmartCoinStatus.Confirmed => "This coin is confirmed.",
			SmartCoinStatus.Unconfirmed => "This coin is unconfirmed.",
			SmartCoinStatus.MixingOnWaitingList => "This coin is waiting for its turn to be coinjoined.",
			SmartCoinStatus.MixingBanned => $"The coordinator banned this coin from participation until {Model?.BannedUntilUtc?.ToString("yyyy - MM - dd HH: mm", CultureInfo.InvariantCulture)}.",
			SmartCoinStatus.MixingInputRegistration => "This coin is registered for coinjoin.",
			SmartCoinStatus.MixingConnectionConfirmation => "This coin is currently in Connection Confirmation phase.",
			SmartCoinStatus.MixingOutputRegistration => "This coin is currently in Output Registration phase.",
			SmartCoinStatus.MixingSigning => "This coin is currently in Signing phase.",
			SmartCoinStatus.SpentAccordingToBackend => "According to the Backend, this coin is spent. Wallet state will be corrected after confirmation.",
			SmartCoinStatus.MixingWaitingForConfirmation => "Coinjoining unconfirmed coins is not allowed, unless the coin is a coinjoin output itself.",
			_ => "This is impossible."
		};

		public Money Amount => Model.Amount;

		public string AmountBtc => Model.Amount.ToString(false, true);

		public string Label => Model.Label.ToString();

		public int Height => Model.Height;

		public string TransactionId => Model.TransactionId.ToString();

		public uint OutputIndex => Model.Index;

		public int AnonymitySet => Model.AnonymitySet;

		public string InCoinJoin => Model.CoinJoinInProgress ? "Yes" : "No";

		public string Clusters => string.IsNullOrEmpty(Model.Clusters) ? "" : Model.Clusters; //If the value is null the bind do not update the view. It shows the previous state for example: ##### even if PrivMode false.

		public string PubKey => Model.HdPubKey?.PubKey?.ToString() ?? "";

		public string KeyPath => Model.HdPubKey?.FullKeyPath?.ToString() ?? "";

		public SmartCoinStatus Status
		{
			get => _status;
			set => this.RaiseAndSetIfChanged(ref _status, value);
		}

		private void RefreshSmartCoinStatus()
		{
			Status = GetSmartCoinStatus();
		}

		private SmartCoinStatus GetSmartCoinStatus()
		{
			Model.SetIsBanned(); // Recheck if the coin's ban has expired.
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
							if (round.State.Phase == Phase.InputRegistration)
							{
								return SmartCoinStatus.MixingInputRegistration;
							}
							else if (round.State.Phase == Phase.ConnectionConfirmation)
							{
								return SmartCoinStatus.MixingConnectionConfirmation;
							}
							else if (round.State.Phase == Phase.OutputRegistration)
							{
								return SmartCoinStatus.MixingOutputRegistration;
							}
							else if (round.State.Phase == Phase.Signing)
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
