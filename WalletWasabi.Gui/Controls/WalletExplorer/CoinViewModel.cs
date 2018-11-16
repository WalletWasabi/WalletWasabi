using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Gui.ViewModels;
using ReactiveUI;
using WalletWasabi.Models;
using NBitcoin;
using System.Reactive.Linq;
using System.Linq;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public enum CoinStatusEnum
	{
		None,
	}
	public class CoinViewModel : ViewModelBase
	{
		private bool _isSelected;
		private CoinStatusEnum _status;

		public CoinViewModel(SmartCoin model)
		{
			Model = model;

			model.WhenAnyValue(x => x.Confirmed).ObserveOn(RxApp.MainThreadScheduler).Subscribe(confirmed =>
			{
				this.RaisePropertyChanged(nameof(Confirmed));
			});

			model.WhenAnyValue(x => x.SpentOrCoinJoinInProgress).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(SpentOrCoinJoinInProgress));
			});

			model.WhenAnyValue(x => x.CoinJoinInProgress).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(CoinJoinInProgress));
			});

			Global.IndexDownloader.WhenAnyValue(x => x.BestHeight).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(Confirmations));
			});
		}

		public SmartCoin Model { get; }

		public bool Confirmed => Model.Confirmed;

		public bool CoinJoinInProgress => Model.CoinJoinInProgress;

		public bool SpentOrCoinJoinInProgress => Model.SpentOrCoinJoinInProgress;

		public string Address => Model.ScriptPubKey.GetDestinationAddress(Global.Network).ToString();

		public int Confirmations => Model.Height.Type == HeightType.Chain
			? Global.IndexDownloader.BestHeight.Value - Model.Height.Value + 1
			: 0;

		public bool IsSelected
		{
			get { return _isSelected; }
			set { this.RaiseAndSetIfChanged(ref _isSelected, value); }
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

		public CoinStatusEnum Status 
		{ 
			get => _status; 
			set { this.RaiseAndSetIfChanged(ref _status, value); } 
		}


	}
}
