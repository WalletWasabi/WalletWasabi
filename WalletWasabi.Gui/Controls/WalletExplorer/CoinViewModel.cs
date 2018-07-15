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
	public class CoinViewModel : ViewModelBase
	{
		private bool _isSelected;
		private int _privacyLevel;

		public CoinViewModel(SmartCoin model)
		{
			Model = model;

			model.WhenAnyValue(x => x.Confirmed).ObserveOn(RxApp.MainThreadScheduler).Subscribe(confirmed =>
			{
				this.RaisePropertyChanged(nameof(Confirmed));
			});

			model.WhenAnyValue(x => x.SpentOrCoinJoinInProcess).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(SpentOrCoinJoinInProcess));
			});

			model.WhenAnyValue(x => x.CoinJoinInProcess).ObserveOn(RxApp.MainThreadScheduler).Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(CoinJoinInProgress));
			});
		}

		public SmartCoin Model { get; }

		public bool Confirmed => Model.Confirmed;

		public bool CoinJoinInProgress => Model.CoinJoinInProcess;

		public bool SpentOrCoinJoinInProcess => Model.SpentOrCoinJoinInProcess;

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

		public int PrivacyLevel
		{
			get { return _privacyLevel; }
			set { this.RaiseAndSetIfChanged(ref _privacyLevel, value); }
		}

		public string TransactionId => Model.TransactionId.ToString();

		public uint OutputIndex => Model.Index;

		public int AnonymitySet => Model.AnonymitySet;

		public string InCoinJoin => Model.CoinJoinInProcess ? "Yes" : "No";

		public string History => string.Join(", ", Global.WalletService.GetHistory(Model, Enumerable.Empty<SmartCoin>()).Select(x => x.Label).Distinct());
	}
}
